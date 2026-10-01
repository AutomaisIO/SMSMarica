using System.Text.Json;
using System.Text.Json.Serialization;

namespace SMSMais.Core.Regulacao.Regras;

/// <summary>
/// Uma opção de pergunta de lista. O <see cref="Id"/> é a identidade (é por ele que a resposta
/// casa com a opção); o <see cref="Texto"/> é o que a pessoa leu — e vai junto na resposta como
/// fotografia, para a resposta continuar legível mesmo depois que a regra mudar.
/// </summary>
[JsonConverter(typeof(OpcaoListaConverter))]
public sealed record OpcaoLista(string Id, string Texto);

/// <summary>
/// O formato JSON das opções das perguntas de lista — <b>versionado</b>.
///
/// <para><b>Por que envelope com versão</b> (pedido do Bernardo, 01/10/2026): as opções moram em
/// <c>jsonb</c> justamente para a estrutura poder crescer sem coluna nova (idade por opção, "outra,
/// descreva", agrupamento). Para isso o JSON precisa dizer de que versão é: quem lê converte o que é
/// antigo, e um backfill leva tudo para a versão atual.</para>
///
/// <list type="bullet">
/// <item><b>v0</b> — lista crua de textos (<c>["A", "B"]</c>), o formato de 01/10/2026 antes do envelope.
/// Lido como opções <c>o1..oN</c>, na ordem.</item>
/// <item><b>v1</b> — <c>{"v":1,"opcoes":[{"id":"o1","texto":"A"}]}</c> na regra e
/// <c>{"v":1,"marcadas":[{"id":"o1","texto":"A"}]}</c> na resposta.</item>
/// </list>
///
/// <para>Ao mudar o formato: suba <see cref="VersaoAtual"/>, ensine o leitor a converter a versão
/// anterior e escreva a migration de backfill (exemplo: <c>OpcoesListaEnvelopeV1</c>).</para>
/// </summary>
public static class OpcoesLista
{
    public const int VersaoAtual = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record EnvelopeRegra(int V, List<OpcaoLista> Opcoes);

    private sealed record EnvelopeResposta(int V, List<OpcaoLista> Marcadas);

    /// <summary>As opções de uma regra. Vazio = pergunta simples (ou JSON ilegível).</summary>
    public static IReadOnlyList<OpcaoLista> LerDaRegra(string? json) => Ler(json, "opcoes");

    /// <summary>As opções marcadas numa resposta.</summary>
    public static IReadOnlyList<OpcaoLista> LerDaResposta(string? json) => Ler(json, "marcadas");

    /// <summary>
    /// Grava as opções de uma regra na versão atual. Os ids nascem na ordem (<c>o1..oN</c>): cada
    /// correção de regra cria versão nova, e a resposta fica presa à versão que foi vista — o id só
    /// precisa ser estável dentro de uma versão.
    /// </summary>
    public static string? GravarDaRegra(IEnumerable<string> textos)
    {
        var opcoes = textos
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select((t, i) => new OpcaoLista($"o{i + 1}", t.Trim()))
            .ToList();
        return opcoes.Count == 0 ? null : JsonSerializer.Serialize(new EnvelopeRegra(VersaoAtual, opcoes), Json);
    }

    public static string? GravarDaResposta(IReadOnlyList<OpcaoLista> marcadas) =>
        marcadas.Count == 0
            ? null
            : JsonSerializer.Serialize(new EnvelopeResposta(VersaoAtual, [.. marcadas]), Json);

    private static IReadOnlyList<OpcaoLista> Ler(string? json, string campo)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;

            // v0: lista crua de textos.
            if (raiz.ValueKind == JsonValueKind.Array) return DeLista(raiz);

            if (raiz.ValueKind == JsonValueKind.Object
                && raiz.TryGetProperty(campo, out var lista)
                && lista.ValueKind == JsonValueKind.Array)
            {
                // v1 (e o que vier depois, enquanto o campo existir): o leitor de cada item aceita
                // texto solto ou objeto, então uma versão nova que só ACRESCENTE campos não quebra.
                return DeLista(lista);
            }
        }
        catch (JsonException)
        {
            // JSON torto não derruba a avaliação: a regra vira pergunta simples.
        }
        return [];
    }

    private static List<OpcaoLista> DeLista(JsonElement lista)
    {
        var opcoes = new List<OpcaoLista>();
        var i = 0;
        foreach (var item in lista.EnumerateArray())
        {
            i++;
            var opcao = OpcaoListaConverter.DeElemento(item, i);
            if (opcao is not null) opcoes.Add(opcao);
        }
        return opcoes;
    }
}

/// <summary>
/// Lê uma opção como objeto <c>{"id","texto"}</c> ou como texto solto (v0). O texto solto aparece
/// em pareceres de análise gravados antes do envelope (<c>regulacao_analise_espelho.alertas_json</c>):
/// sem isto eles deixariam de abrir até a próxima reanálise.
/// </summary>
public sealed class OpcaoListaConverter : JsonConverter<OpcaoLista>
{
    public override OpcaoLista? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return DeElemento(doc.RootElement, 0);
    }

    public override void Write(Utf8JsonWriter writer, OpcaoLista value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(options.PropertyNamingPolicy?.ConvertName("Id") ?? "Id", value.Id);
        writer.WriteString(options.PropertyNamingPolicy?.ConvertName("Texto") ?? "Texto", value.Texto);
        writer.WriteEndObject();
    }

    internal static OpcaoLista? DeElemento(JsonElement item, int posicao)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            var texto = item.GetString();
            return string.IsNullOrWhiteSpace(texto) ? null : new OpcaoLista($"o{posicao}", texto.Trim());
        }
        if (item.ValueKind != JsonValueKind.Object) return null;

        string? Prop(string nome) =>
            item.EnumerateObject()
                .FirstOrDefault(p => string.Equals(p.Name, nome, StringComparison.OrdinalIgnoreCase))
                .Value is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

        var textoObj = Prop("texto");
        if (string.IsNullOrWhiteSpace(textoObj)) return null;
        return new OpcaoLista(Prop("id") ?? $"o{posicao}", textoObj.Trim());
    }
}

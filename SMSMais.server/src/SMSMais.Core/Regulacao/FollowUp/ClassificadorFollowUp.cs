using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SMSMais.Core.Regulacao.FollowUp;

/// <summary>
/// Uma regra de classificação de follow-up, como fica gravada em
/// <c>regulacao_configuracao.regras_followup_json</c>.
/// </summary>
/// <param name="Categoria">Uma das nove medidas no spike d.</param>
/// <param name="Ordem">Menor primeiro. A ordem é parte da regra, não enfeite (ver a classe).</param>
/// <param name="Padrao">Regex já escrita sobre o texto <b>normalizado</b> (sem acento, maiúsculo).</param>
/// <param name="ViraPendencia">
/// <c>"contato"</c>, <c>"documento"</c> ou <c>null</c>. Só duas das nove categorias pedem ação da
/// unidade; as outras sete são estado, e transformá-las em pendência encheria a fila de itens que
/// ninguém pode resolver — 5,5× itens falsos, medido no spike d.
/// </param>
public sealed record RegraFollowUp(
    [property: JsonPropertyName("categoria")] string Categoria,
    [property: JsonPropertyName("ordem")] int Ordem,
    [property: JsonPropertyName("padrao")] string Padrao,
    [property: JsonPropertyName("vira_pendencia")] string? ViraPendencia);

/// <summary>
/// Classificador de follow-up do SER/SERNIT: texto livre entra, categoria sai. <b>Função pura</b> —
/// sem banco, sem HTTP, sem relógio —, para poder ser testada sobre o corpus real e reexecutada
/// à vontade pela caixa de teste da tela de configuração.
///
/// <para><b>Por que regex e não IA:</b> sobre 18.904 follow-ups reais (spike d), regex simples
/// deixam só 13% em "Outro", com precisão medida de 100% nas sete classes amostradas. Chamar
/// modelo para isso seria custo e latência sem ganho.</para>
///
/// <para><b>Por que a ordem importa:</b> os textos empilham assunto — "sem contato, paciente segue
/// em fila de espera" casa `FalhaContato` <i>e</i> `SemVaga`. Quem vem primeiro decide, e a
/// primeira é a que pede ação da unidade. Inverter a ordem não muda o rótulo por capricho: muda
/// se a solicitação vira pendência ou não.</para>
///
/// <para><b>Por que normalizar antes:</b> o campo é digitado à mão por centenas de operadores —
/// "NÃO ATENDE", "nao atende", "Não  Atende" são a mesma coisa. As regras do spike d são escritas
/// sobre a forma normalizada; aplicá-las ao texto cru perderia a maioria das ocorrências.</para>
/// </summary>
public static class ClassificadorFollowUp
{
    /// <summary>Devolvida quando nada casa. 13% do corpus real cai aqui, e tudo bem.</summary>
    public const string CategoriaPadrao = "Outro";

    /// <summary>
    /// Teto por regra. Uma regex ruim salva pela tela não pode travar a varredura noturna: o
    /// estouro descarta <i>aquela</i> regra e a classificação continua nas seguintes.
    /// </summary>
    private static readonly TimeSpan LimiteRegex = TimeSpan.FromMilliseconds(250);

    private static readonly Regex EspacosSeguidos = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// NFKD sem acento, espaços colapsados, maiúsculo — a mesma normalização usada para medir as
    /// regras no spike d. Mudar isto aqui invalida os padrões gravados.
    /// </summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var decomposto = texto.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return EspacosSeguidos.Replace(sb.ToString(), " ").Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Primeira regra que casa vence. Lista vazia = classificador desligado, tudo cai em "Outro" —
    /// que é o estado de fábrica: sem curadoria, nada vira pendência sozinho.
    /// </summary>
    public static ResultadoFollowUp Classificar(string? texto, IReadOnlyList<RegraFollowUp> regras)
    {
        var normalizado = Normalizar(texto);
        if (normalizado.Length == 0 || regras.Count == 0)
        {
            return new ResultadoFollowUp(CategoriaPadrao, null, null);
        }

        foreach (var regra in regras.OrderBy(r => r.Ordem))
        {
            if (string.IsNullOrWhiteSpace(regra.Padrao)) continue;

            try
            {
                if (Regex.IsMatch(normalizado, regra.Padrao, RegexOptions.None, LimiteRegex))
                {
                    return new ResultadoFollowUp(regra.Categoria, regra.ViraPendencia, regra.Ordem);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Regra cara demais para este texto. Vale mais classificar pelas outras do que
                // devolver "Outro" para tudo que vier depois dela.
            }
            catch (ArgumentException)
            {
                // Regex inválida. O validador barra na gravação; se chegou aqui, foi gravada
                // antes da validação existir — ignorar é melhor que derrubar a varredura.
            }
        }

        return new ResultadoFollowUp(CategoriaPadrao, null, null);
    }

    /// <summary>Lê o JSON da configuração. Conteúdo inválido vira lista vazia, nunca exceção.</summary>
    public static IReadOnlyList<RegraFollowUp> Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<RegraFollowUp>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <param name="Categoria">Uma das nove, ou "Outro".</param>
/// <param name="ViraPendencia">O que o incremento 6 deve abrir, ou <c>null</c>.</param>
/// <param name="OrdemDaRegra">Qual regra decidiu — é o que a caixa de teste da tela mostra.</param>
public sealed record ResultadoFollowUp(string Categoria, string? ViraPendencia, int? OrdemDaRegra);

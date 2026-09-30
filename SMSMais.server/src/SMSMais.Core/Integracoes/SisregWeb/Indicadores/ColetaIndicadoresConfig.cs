using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;

namespace SMSMais.Core.Integracoes.SisregWeb.Indicadores;

/// <summary>O coletor dos indicadores: ligado ou não, e até quando está pausado (CAPTCHA).</summary>
public sealed record ColetaIndicadoresConfigDto(bool Ativa, DateTime? PausadaAte);

public sealed record SalvarColetaIndicadoresRequest(bool Ativa);

/// <summary>
/// Onde mora o liga/desliga do coletor: no <c>ParametrosJson</c> da credencial <c>sisreg</c>, o mesmo
/// lugar do horário da fila, das escalas e do lote de mapeamento. Sem tabela nova, sem migration.
///
/// <para><b>Nasce DESLIGADO.</b> O passado (jan/2025–ago/2026) foi carregado pelo laboratório em
/// 30/09/2026; ligar é decisão de quem cuida da integração, porque cada leitura sai do mesmo
/// orçamento anti-robô do operador que o robô de produção usa.</para>
///
/// <para>A pausa de CAPTCHA também fica aqui (e não em memória): um restart logo depois de um CAPTCHA
/// não pode voltar a bater no SISREG com o operador bloqueado.</para>
/// </summary>
public static class ColetaIndicadoresConfig
{
    public const string ChaveAtiva = "indicadoresColetaAtiva";
    public const string ChavePausadaAte = "indicadoresColetaPausadaAte";

    public static ColetaIndicadoresConfigDto Ler(JsonObject? json)
    {
        var ativa = false;
        if (json?[ChaveAtiva] is JsonValue a && a.TryGetValue<bool>(out var b)) ativa = b;

        DateTime? pausa = null;
        if (json?[ChavePausadaAte] is JsonValue p && p.TryGetValue<string>(out var s)
            && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d))
        {
            pausa = DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }

        return new ColetaIndicadoresConfigDto(ativa, pausa);
    }

    public static async Task<ColetaIndicadoresConfigDto> ObterAsync(
        IIntegracaoCredencialService credenciais, CancellationToken ct)
    {
        try
        {
            var atual = await credenciais.ObterAsync(SisregWebSessao.Provedor, ct);
            return Ler(Parse(atual.ParametrosJson));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Credencial ausente ou ilegível: desligado — o coletor nunca liga por omissão.
            return Ler(null);
        }
    }

    public static Task<ColetaIndicadoresConfigDto> SalvarAsync(
        IIntegracaoCredencialService credenciais, SalvarColetaIndicadoresRequest request, CancellationToken ct) =>
        AlterarAsync(credenciais, json => json[ChaveAtiva] = request.Ativa, ct);

    /// <summary>Pausa até <paramref name="ate"/> (UTC); <c>null</c> retoma.</summary>
    public static Task<ColetaIndicadoresConfigDto> PausarAsync(
        IIntegracaoCredencialService credenciais, DateTime? ate, CancellationToken ct) =>
        AlterarAsync(credenciais, json =>
        {
            if (ate is { } a) json[ChavePausadaAte] = a.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            else json.Remove(ChavePausadaAte);
        }, ct);

    private static async Task<ColetaIndicadoresConfigDto> AlterarAsync(
        IIntegracaoCredencialService credenciais, Action<JsonObject> alterar, CancellationToken ct)
    {
        // Merge: usuário, senha, fila, escalas e mapeamento vivem no MESMO ParametrosJson —
        // sobrescrever o JSON inteiro apagaria a credencial de acesso.
        var atual = await credenciais.ObterAsync(SisregWebSessao.Provedor, ct);
        var json = Parse(atual.ParametrosJson) ?? new JsonObject();
        alterar(json);

        await credenciais.AtualizarAsync(
            SisregWebSessao.Provedor,
            new AtualizarIntegracaoCredencialRequest(
                ClientId: null,
                ClientSecret: null,
                RedirectUri: atual.RedirectUri,
                ParametrosJson: json.ToJsonString(),
                Ativo: atual.Ativo),
            ct);

        return Ler(json);
    }

    private static JsonObject? Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        try
        {
            return JsonNode.Parse(texto) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

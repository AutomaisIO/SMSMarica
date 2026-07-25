using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SMSMarica.Secretario.Api.Oracle;

/// <summary>
/// Acesso às bases pelo <b>proxy SQL interno do smsmarica</b> (porta de loopback + token).
///
/// O painel deixou de falar direto com o Oracle: não guarda mais credencial de banco, não
/// carrega driver e não precisa saber o dialeto. Manda o slug da base cadastrada e o SQL; o
/// smsmarica alcança cada base do jeito que ela exige — Oracle pelo túnel (HMCML) ou SQL
/// Server pelo agente WSS reverso (UPA). Ver ADR-0023 e Interno/ProxySqlEndpoint.cs no server.
///
/// Read-only é garantido do outro lado (SqlReadOnlyGuard, e de novo no agente); aqui o guard
/// local continua como primeira barreira — erro de SQL nosso morre antes de sair da máquina.
///
/// Resiliência: o proxy é local (loopback), então falha aqui é o smsmarica reiniciando ou o
/// agente da base remota fora do ar. Uma tentativa extra cobre a janela curta de restart; o
/// resto é reportado como falha do ciclo e o snapshot anterior continua servindo.
/// </summary>
public sealed class ProxySqlFonte(HttpClient http, string token, int maxLinhas)
{
    private const int MaxTentativas = 2;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Executa as consultas na ordem dada, numa única ida ao proxy.</summary>
    public async Task<IReadOnlyList<ResultadoConsulta>> ExecutarAsync(
        string baseSlug, IReadOnlyList<string> consultas, CancellationToken ct = default)
    {
        foreach (var sql in consultas)
        {
            SqlReadOnlyGuard.GarantirLeitura(sql);
        }

        var corpo = new ProxyRequisicao(baseSlug, consultas, maxLinhas);

        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/proxy-sql")
                {
                    Content = JsonContent.Create(corpo, options: Json),
                };
                requisicao.Headers.Add("X-Proxy-Token", token);

                using var resposta = await http.SendAsync(requisicao, ct);

                if (!resposta.IsSuccessStatusCode)
                {
                    var detalhe = await resposta.Content.ReadAsStringAsync(ct);
                    throw new InvalidOperationException(
                        $"Proxy SQL respondeu {(int)resposta.StatusCode}: {Resumir(detalhe)}");
                }

                var payload = await resposta.Content.ReadFromJsonAsync<ProxyResposta>(Json, ct)
                    ?? throw new InvalidOperationException("Proxy SQL devolveu corpo vazio.");

                return
                [
                    .. payload.Resultados.Select(r => new ResultadoConsulta(
                        Ok: true,
                        Colunas: r.Colunas,
                        Linhas: [.. r.Linhas.Select(linha => (IReadOnlyList<object?>)[.. linha.Select(Converter)])])),
                ];
            }
            catch (Exception ex) when (
                tentativa < MaxTentativas && ex is HttpRequestException or TaskCanceledException
                && !ct.IsCancellationRequested)
            {
                // smsmarica reiniciando (deploy) — janela curta, uma segunda chance basta.
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }

    /// <summary>Conveniência para consulta única.</summary>
    public async Task<ResultadoConsulta> ExecutarUmaAsync(
        string baseSlug, string sql, CancellationToken ct = default) =>
        (await ExecutarAsync(baseSlug, [sql], ct))[0];

    private static string Resumir(string texto) =>
        texto.Length <= 200 ? texto : texto[..200] + "…";

    /// <summary>
    /// JSON de volta para tipo nativo. Datas importam: o montador das séries testa
    /// <c>linha[0] is DateTime</c>, e no JSON elas chegam como texto ISO.
    /// </summary>
    private static object? Converter(JsonElement? valor)
    {
        if (valor is not { } v)
        {
            return null;
        }

        switch (v.ValueKind)
        {
            case JsonValueKind.Null or JsonValueKind.Undefined:
                return null;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                return v.TryGetInt64(out var inteiro) ? inteiro : v.GetDouble();
            case JsonValueKind.String:
                var texto = v.GetString();
                // ISO ("2026-07-24T00:00:00") vira DateTime; o resto segue texto.
                return DateTime.TryParse(
                    texto, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var data)
                    && texto is { Length: >= 10 } && texto[4] == '-' && texto[7] == '-'
                    ? data
                    : texto;
            default:
                return v.ToString();
        }
    }

    private sealed record ProxyRequisicao(
        string Base, IReadOnlyList<string> Consultas, [property: JsonPropertyName("maxLinhas")] int MaxLinhas);

    private sealed record ProxyResposta(IReadOnlyList<ProxyResultado> Resultados);

    private sealed record ProxyResultado(
        IReadOnlyList<string> Colunas, IReadOnlyList<IReadOnlyList<JsonElement?>> Linhas, long DuracaoMs);
}

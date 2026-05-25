using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SMSMarica.Core.Worklist;

/// <summary>
/// Implementação leve do QIDO-RS — só checa existência. HttpClient configurado
/// com a base do AE <c>DCM4CHEE</c> (não o WORKLIST).
/// </summary>
public sealed class ConsultaStudyClient(HttpClient http, ILogger<ConsultaStudyClient> logger) : IConsultaStudyClient
{
    private readonly HttpClient _http = http;
    private readonly ILogger<ConsultaStudyClient> _logger = logger;

    public async Task<bool> StudyExisteAsync(string accessionNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessionNumber)) return false;

        var url = $"studies?AccessionNumber={Uri.EscapeDataString(accessionNumber)}&limit=1";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dicom+json"));

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.SendAsync(req, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QIDO-RS indisponível ao checar AccessionNumber {Acc}.", accessionNumber);
            return false;
        }

        // 204 No Content significa "nenhum match" — perfeito.
        if (resposta.StatusCode == System.Net.HttpStatusCode.NoContent) return false;
        if (!resposta.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "QIDO-RS retornou {Status} para AccessionNumber {Acc}.",
                (int)resposta.StatusCode, accessionNumber);
            return false;
        }

        await using var stream = await resposta.Content.ReadAsStreamAsync(cancellationToken);
        try
        {
            var arr = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: cancellationToken);
            return arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

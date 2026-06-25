using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SMSMarica.Core.Worklist;

/// <summary>
/// Implementação leve do QIDO-RS. HttpClient configurado com a base do AE
/// <c>PACS-CDT</c> (não o WORK-CDT).
/// </summary>
public sealed class ConsultaStudyClient(HttpClient http, ILogger<ConsultaStudyClient> logger) : IConsultaStudyClient
{
    private readonly HttpClient _http = http;
    private readonly ILogger<ConsultaStudyClient> _logger = logger;

    public async Task<bool> StudyExisteAsync(string accessionNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessionNumber)) return false;
        var arr = await ConsultarAsync(
            $"studies?AccessionNumber={Uri.EscapeDataString(accessionNumber)}&limit=1", cancellationToken);
        return arr is { ValueKind: JsonValueKind.Array } a && a.GetArrayLength() > 0;
    }

    public async Task<bool> StudyExistePorStudyUidAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID)) return false;
        var arr = await ConsultarAsync(
            $"studies?StudyInstanceUID={Uri.EscapeDataString(studyInstanceUID)}&limit=1", cancellationToken);
        return arr is { ValueKind: JsonValueKind.Array } a && a.GetArrayLength() > 0;
    }

    public async Task<IReadOnlyList<EstudoPacsBasico>> BuscarPorPatientIdAsync(string patientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(patientId)) return [];
        var arr = await ConsultarAsync(
            $"studies?PatientID={Uri.EscapeDataString(patientId)}&includefield=00080050&includefield=0020000D&limit=100",
            cancellationToken);
        if (arr is not { ValueKind: JsonValueKind.Array } a) return [];

        var lista = new List<EstudoPacsBasico>(a.GetArrayLength());
        foreach (var estudo in a.EnumerateArray())
        {
            var uid = Tag(estudo, "0020000D");
            if (string.IsNullOrWhiteSpace(uid)) continue;
            lista.Add(new EstudoPacsBasico(uid!, Tag(estudo, "00080050")));
        }
        return lista;
    }

    /// <summary>Faz o GET QIDO-RS e devolve o array JSON (ou null em falha/204).</summary>
    private async Task<JsonElement?> ConsultarAsync(string url, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dicom+json"));

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.SendAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QIDO-RS indisponível em {Url}.", url);
            return null;
        }

        if (resposta.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        if (!resposta.IsSuccessStatusCode)
        {
            _logger.LogWarning("QIDO-RS retornou {Status} em {Url}.", (int)resposta.StatusCode, url);
            return null;
        }

        await using var stream = await resposta.Content.ReadAsStreamAsync(ct);
        try
        {
            return await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Lê o primeiro valor de uma tag DICOM-JSON (ex.: "0020000D").</summary>
    private static string? Tag(JsonElement estudo, string tag)
    {
        if (estudo.ValueKind != JsonValueKind.Object) return null;
        if (!estudo.TryGetProperty(tag, out var campo)) return null;
        if (!campo.TryGetProperty("Value", out var valor) || valor.ValueKind != JsonValueKind.Array || valor.GetArrayLength() == 0)
            return null;
        var primeiro = valor[0];
        return primeiro.ValueKind == JsonValueKind.String ? primeiro.GetString() : primeiro.ToString();
    }
}

using System.Text.Json;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Pacs;

namespace SMSMarica.Core.Exames;

/// <summary>
/// Lê as imagens de um estudo no PACS (QIDO-RS para listar instâncias + WADO-RS
/// /rendered para baixar cada JPEG já rasterizado pelo dcm4chee). Compartilhado
/// pelos PDFs de imagens e de exame completo.
/// </summary>
public interface IExamePacsImagensReader
{
    /// <summary>
    /// JPEGs renderizados do estudo, ordenados por série/instância. Lista vazia se o
    /// estudo não existir/sem imagens. Trunca em <paramref name="maxImagens"/>.
    /// </summary>
    Task<IReadOnlyList<byte[]>> ObterImagensAsync(string studyInstanceUID, int maxImagens, CancellationToken cancellationToken = default);
}

public sealed class ExamePacsImagensReader(
    IPacsProxyService pacs,
    ILogger<ExamePacsImagensReader> logger) : IExamePacsImagensReader
{
    private const string DicomJson = "application/dicom+json";
    private const string Jpeg = "image/jpeg";

    public async Task<IReadOnlyList<byte[]>> ObterImagensAsync(
        string studyInstanceUID, int maxImagens, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID)) return [];

        var instancias = await ListarInstanciasAsync(studyInstanceUID, cancellationToken);
        if (instancias.Count == 0) return [];

        if (instancias.Count > maxImagens)
        {
            logger.LogWarning(
                "Estudo {Study} tem {Total} instâncias; truncando para {Max} no PDF.",
                studyInstanceUID, instancias.Count, maxImagens);
            instancias = instancias.Take(maxImagens).ToList();
        }

        var imagens = new List<byte[]>(instancias.Count);
        foreach (var inst in instancias)
        {
            var jpeg = await ObterImagemRenderizadaAsync(
                studyInstanceUID, inst.SeriesUid, inst.SopUid, cancellationToken);
            if (jpeg is { Length: > 0 })
                imagens.Add(jpeg);
        }
        return imagens;
    }

    private sealed record Instancia(string SeriesUid, string SopUid, int SeriesNumero, int InstanciaNumero);

    /// <summary>QIDO-RS: lista as instâncias do estudo (série + SOP), ordenadas por série/instância.</summary>
    private async Task<List<Instancia>> ListarInstanciasAsync(string studyUid, CancellationToken ct)
    {
        using var resposta = await pacs.EncaminharAsync(
            HttpMethod.Get,
            $"studies/{studyUid}/instances",
            "?includefield=00200011&includefield=00200013",
            DicomJson,
            ct);

        if (!resposta.IsSuccessStatusCode)
        {
            if (resposta.StatusCode == System.Net.HttpStatusCode.NoContent) return [];
            logger.LogWarning("QIDO de instâncias falhou ({Status}) para o estudo {Study}.",
                (int)resposta.StatusCode, studyUid);
            return [];
        }

        // dcm4chee às vezes responde 200 com corpo VAZIO (estudo inexistente/sem
        // instâncias) em vez de 204 — não tente parsear JSON nesse caso.
        var corpo = await resposta.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(corpo)) return [];

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(corpo);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "QIDO de instâncias retornou corpo não-JSON para o estudo {Study}.", studyUid);
            return [];
        }

        using var _ = doc;
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

        var lista = new List<Instancia>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var series = ValorTag(item, "0020000E");
            var sop = ValorTag(item, "00080018");
            if (string.IsNullOrEmpty(series) || string.IsNullOrEmpty(sop)) continue;
            lista.Add(new Instancia(
                series, sop,
                InteiroTag(item, "00200011"),
                InteiroTag(item, "00200013")));
        }

        return [.. lista.OrderBy(i => i.SeriesNumero).ThenBy(i => i.InstanciaNumero)];
    }

    /// <summary>WADO-RS /rendered: imagem JPEG já rasterizada pelo dcm4chee.</summary>
    private async Task<byte[]?> ObterImagemRenderizadaAsync(
        string studyUid, string seriesUid, string sopUid, CancellationToken ct)
    {
        using var resposta = await pacs.EncaminharAsync(
            HttpMethod.Get,
            $"studies/{studyUid}/series/{seriesUid}/instances/{sopUid}/rendered",
            string.Empty,
            Jpeg,
            ct);

        if (!resposta.IsSuccessStatusCode)
        {
            logger.LogWarning("WADO /rendered falhou ({Status}) para a instância {Sop}.",
                (int)resposta.StatusCode, sopUid);
            return null;
        }

        return await resposta.Content.ReadAsByteArrayAsync(ct);
    }

    private static string? ValorTag(JsonElement item, string tag) =>
        item.TryGetProperty(tag, out var campo)
        && campo.TryGetProperty("Value", out var valor)
        && valor.ValueKind == JsonValueKind.Array
        && valor.GetArrayLength() > 0
            ? valor[0].GetString()
            : null;

    private static int InteiroTag(JsonElement item, string tag) =>
        int.TryParse(ValorTag(item, tag), out var n) ? n : 0;
}

using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Armazenamento;
using SMSMarica.Core.Pacs;

namespace SMSMarica.Core.Exames;

/// <summary>
/// Lê as imagens de um estudo no PACS (QIDO-RS para listar instâncias + WADO-RS
/// /rendered para baixar cada JPEG já rasterizado pelo dcm4chee). Compartilhado
/// pelos PDFs de imagens e de exame completo.
/// <para>
/// CACHE por estudo no S3 (<c>render-cache/{study}.zip</c>): rasterizar cada DICOM
/// (~53MB na mamografia) é a operação mais cara do PACS — cacheado, os PDFs saem
/// sem tocar o dcm4chee. O cache é aquecido de fundo pelo
/// <see cref="Background.PreparadorImagensExameService"/> quando o exame chega, e
/// preenchido on-demand no primeiro acesso dos demais casos. Falha de cache nunca
/// quebra o fluxo: cai no caminho direto ao PACS.
/// </para>
/// </summary>
public interface IExamePacsImagensReader
{
    /// <summary>
    /// JPEGs renderizados do estudo, ordenados por série/instância. Lista vazia se o
    /// estudo não existir/sem imagens. Trunca em <paramref name="maxImagens"/>.
    /// </summary>
    Task<IReadOnlyList<byte[]>> ObterImagensAsync(string studyInstanceUID, int maxImagens, CancellationToken cancellationToken = default);

    /// <summary>Nº de instâncias do estudo no PACS AGORA (QIDO). 0 se inexistente/indisponível.</summary>
    Task<int> ContarInstanciasPacsAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>Nº de imagens guardadas no render-cache do estudo. <c>null</c> se não há cache.</summary>
    Task<int?> ContarImagensCacheadasAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>Apaga o render-cache do estudo (idempotente). A próxima leitura re-materializa do PACS.</summary>
    Task InvalidarCacheAsync(string studyInstanceUID, CancellationToken cancellationToken = default);
}

public sealed class ExamePacsImagensReader(
    IPacsProxyService pacs,
    IArmazenamentoArquivos armazenamento,
    ILogger<ExamePacsImagensReader> logger) : IExamePacsImagensReader
{
    private const string DicomJson = "application/dicom+json";
    private const string Jpeg = "image/jpeg";

    public async Task<IReadOnlyList<byte[]>> ObterImagensAsync(
        string studyInstanceUID, int maxImagens, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID)) return [];

        var chaveCache = $"render-cache/{studyInstanceUID.Trim()}.zip";

        var doCache = await LerCacheAsync(chaveCache, cancellationToken);
        if (doCache is { Count: > 0 })
            return doCache.Count > maxImagens ? doCache.Take(maxImagens).ToList() : doCache;

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

        // Só cacheia o conjunto COMPLETO (toda instância pedida rasterizou) — cachear um
        // conjunto parcial (ex.: /rendered falhou numa instância) congelaria o PDF capenga.
        if (imagens.Count > 0 && imagens.Count == instancias.Count)
            await GravarCacheAsync(chaveCache, imagens, cancellationToken);

        return imagens;
    }

    public async Task<int> ContarInstanciasPacsAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID)) return 0;
        var instancias = await ListarInstanciasAsync(studyInstanceUID.Trim(), cancellationToken);
        return instancias.Count;
    }

    public async Task<int?> ContarImagensCacheadasAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID)) return null;
        var chaveCache = $"render-cache/{studyInstanceUID.Trim()}.zip";
        try
        {
            var zip = await armazenamento.LerAsync(chaveCache, cancellationToken);
            if (zip is not { Length: > 0 }) return null;
            // Conta as entradas pelo diretório central do zip — não descomprime os JPEGs.
            using var arquivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            return arquivo.Entries.Count(e => e.Length > 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao contar o cache de imagens {Chave}.", chaveCache);
            return null;
        }
    }

    public async Task InvalidarCacheAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID)) return;
        await armazenamento.ExcluirAsync($"render-cache/{studyInstanceUID.Trim()}.zip", cancellationToken);
    }

    // ---------------- Cache S3 (zip de JPEGs, ordem preservada pelo nome da entrada) ----------------

    private async Task<IReadOnlyList<byte[]>?> LerCacheAsync(string chave, CancellationToken ct)
    {
        try
        {
            var zip = await armazenamento.LerAsync(chave, ct);
            if (zip is not { Length: > 0 }) return null;

            using var arquivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            var imagens = new List<byte[]>(arquivo.Entries.Count);
            foreach (var entrada in arquivo.Entries.OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                using var ms = new MemoryStream((int)entrada.Length);
                await using (var s = entrada.Open())
                {
                    await s.CopyToAsync(ms, ct);
                }
                if (ms.Length > 0) imagens.Add(ms.ToArray());
            }
            return imagens;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao ler o cache de imagens {Chave} — indo direto ao PACS.", chave);
            return null;
        }
    }

    private async Task GravarCacheAsync(string chave, IReadOnlyList<byte[]> imagens, CancellationToken ct)
    {
        try
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                for (var i = 0; i < imagens.Count; i++)
                {
                    // JPEG já é comprimido — o zip é só contêiner ordenado (sem recomprimir).
                    var entrada = zip.CreateEntry($"{i + 1:D4}.jpg", CompressionLevel.NoCompression);
                    await using var s = entrada.Open();
                    await s.WriteAsync(imagens[i], ct);
                }
            }
            await armazenamento.SalvarAsync(chave, ms.ToArray(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao gravar o cache de imagens {Chave} — seguindo sem cache.", chave);
        }
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

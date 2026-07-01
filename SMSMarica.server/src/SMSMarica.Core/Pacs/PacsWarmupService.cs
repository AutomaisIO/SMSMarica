using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SMSMarica.Core.Pacs;

/// <summary>
/// Implementação do pré-aquecimento. Usa o próprio <see cref="IPacsProxyService"/>
/// para falar QIDO-RS (séries/instâncias) e WADO-RS (frames), gravando os bytes
/// no <see cref="IPacsCache"/> com a MESMA chave que o GET <c>/pacs/rs</c> usaria —
/// assim a primeira visualização do médico já cai em cache hit.
/// </summary>
public sealed class PacsWarmupService(
    IPacsProxyService pacs,
    IPacsCache cache,
    IPacsTranscodeService transcode,
    ILogger<PacsWarmupService> logger) : IPacsWarmupService
{
    private const string AcceptJson = "application/dicom+json";

    // Accept padrão de frame WADO-RS (octet-stream). Se houver transfer-syntax
    // preferido configurado, o próprio proxy reescreve este Accept (igual ao GET do
    // visualizador), e a chave do cache carrega o mesmo discriminante — então o que
    // o warmup grava é exatamente o que o GET vai ler.
    private const string AcceptFrame = "multipart/related; type=\"application/octet-stream\"";

    // Limite de downloads simultâneos de frames para não saturar o PACS.
    private const int Concorrencia = 4;

    private readonly IPacsProxyService _pacs = pacs;
    private readonly IPacsCache _cache = cache;
    private readonly IPacsTranscodeService _transcode = transcode;
    private readonly ILogger<PacsWarmupService> _logger = logger;

    public async Task AquecerEstudoAsync(string studyUid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyUid)) return;
        if (!_cache.Habilitado) return; // sem cache, aquecer não tem efeito

        var series = await ListarValoresAsync(
            $"studies/{studyUid}/series",
            "0020000E", // Series Instance UID
            cancellationToken);

        using var limite = new SemaphoreSlim(Concorrencia);
        var tarefas = new List<Task>();

        foreach (var seriesUid in series)
        {
            var instancias = await ListarValoresAsync(
                $"studies/{studyUid}/series/{seriesUid}/instances",
                "00080018", // SOP Instance UID
                cancellationToken);

            foreach (var sopUid in instancias)
            {
                var caminho = $"studies/{studyUid}/series/{seriesUid}/instances/{sopUid}/frames/1";
                tarefas.Add(AquecerFrameAsync(caminho, limite, cancellationToken));
            }
        }

        await Task.WhenAll(tarefas);
    }

    // Sentinela (idêntico ao do proxy) que pede o frame cru, desviando do transcode.
    private const string SentinelaSemCompressao = "?semCompressao=1";

    public async Task AquecerInstanciaAsync(
        string studyUid, string seriesUid, string sopUid,
        bool semCompressao = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyUid)
            || string.IsNullOrWhiteSpace(seriesUid)
            || string.IsNullOrWhiteSpace(sopUid)) return;
        if (!_cache.Habilitado) return;

        var caminho = $"studies/{studyUid}/series/{seriesUid}/instances/{sopUid}/frames/1";

        if (semCompressao)
        {
            await AquecerFrameCruAsync(caminho, cancellationToken);
            return;
        }

        using var limite = new SemaphoreSlim(1);
        await AquecerFrameAsync(caminho, limite, cancellationToken);
    }

    /// <summary>
    /// Aquece o frame CRU sob a chave sentinela (<c>?semCompressao=1</c>), buscando
    /// no dcm4chee sem o sentinela (que ele não conhece) — igual ao proxy. Best-effort.
    /// </summary>
    private async Task AquecerFrameCruAsync(string caminho, CancellationToken ct)
    {
        try
        {
            var chave = _cache.CalcularChave("GET", caminho, SentinelaSemCompressao);
            if (_cache.Contains(chave)) return; // já aquecido

            // Upstream recebe query vazia (o sentinela é só interno).
            using var resposta = await _pacs.EncaminharAsync(
                HttpMethod.Get, caminho, queryString: string.Empty, accept: AcceptFrame, ct);
            if (!resposta.IsSuccessStatusCode) return;

            var teto = _cache.TetoItemBytes > 0 ? _cache.TetoItemBytes : StreamLimitado.TetoSegurancaPadrao;
            await using var origem = await resposta.Content.ReadAsStreamAsync(ct);
            var (buffer, completo) = await StreamLimitado.LerComTetoAsync(origem, teto, ct);
            if (!completo) return; // grande demais para cachear

            var contentType = resposta.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            _cache.Set(chave, contentType, buffer);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao aquecer frame cru {Caminho}.", caminho);
        }
    }

    /// <summary>Puxa um frame pelo proxy e grava no cache (best-effort).</summary>
    private async Task AquecerFrameAsync(string caminho, SemaphoreSlim limite, CancellationToken ct)
    {
        await limite.WaitAsync(ct);
        try
        {
            // Compressão ligada: aquece a variante JPEG-LS (mesma chave que o GET serve).
            if (_transcode.Habilitado)
            {
                var chaveComprimida = _cache.CalcularChave("GET", _transcode.DiscriminarCaminho(caminho), string.Empty);
                if (_cache.Contains(chaveComprimida)) return; // já aquecido

                var comprimido = await _transcode.TranscodificarFrameAsync(caminho, ct);
                if (comprimido is not null)
                {
                    _cache.Set(chaveComprimida, comprimido.ContentType, comprimido.Conteudo);
                    return;
                }
                // Transcode falhou: cai no aquecimento do frame cru (fallback do GET).
            }

            var chave = _cache.CalcularChave("GET", caminho, string.Empty);
            if (_cache.Contains(chave)) return; // já aquecido (checagem leve, sem ler bytes)

            using var resposta = await _pacs.EncaminharAsync(
                HttpMethod.Get, caminho, queryString: string.Empty, accept: AcceptFrame, ct);
            if (!resposta.IsSuccessStatusCode) return;

            // Leitura com teto RÍGIDO (sem depender de Content-Length): se o frame
            // estourar o teto por item, não cacheia (e nem segura o buffer em RAM).
            var teto = _cache.TetoItemBytes > 0 ? _cache.TetoItemBytes : StreamLimitado.TetoSegurancaPadrao;
            await using var origem = await resposta.Content.ReadAsStreamAsync(ct);
            var (buffer, completo) = await StreamLimitado.LerComTetoAsync(origem, teto, ct);
            if (!completo) return; // grande demais para cachear

            var contentType = resposta.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            _cache.Set(chave, contentType, buffer);
        }
        catch (Exception ex)
        {
            // Best-effort: uma imagem falhar não pode abortar o aquecimento das demais.
            _logger.LogWarning(ex, "Falha ao aquecer frame {Caminho}.", caminho);
        }
        finally
        {
            limite.Release();
        }
    }

    /// <summary>
    /// QIDO-RS: lista um valor de tag (ex.: Series/SOP UID) de cada item do array.
    /// </summary>
    private async Task<IReadOnlyList<string>> ListarValoresAsync(string caminho, string tag, CancellationToken ct)
    {
        try
        {
            using var resposta = await _pacs.EncaminharAsync(
                HttpMethod.Get, caminho, queryString: $"?includefield={tag}&limit=10000", accept: AcceptJson, ct);
            if (!resposta.IsSuccessStatusCode) return [];

            await using var stream = await resposta.Content.ReadAsStreamAsync(ct);
            var doc = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: ct);
            if (doc.ValueKind != JsonValueKind.Array) return [];

            var lista = new List<string>(doc.GetArrayLength());
            foreach (var item in doc.EnumerateArray())
            {
                var valor = LerTag(item, tag);
                if (!string.IsNullOrWhiteSpace(valor)) lista.Add(valor!);
            }
            return lista;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao listar {Tag} em {Caminho}.", tag, caminho);
            return [];
        }
    }

    /// <summary>Lê o primeiro valor de uma tag DICOM-JSON.</summary>
    private static string? LerTag(JsonElement item, string tag)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;
        if (!item.TryGetProperty(tag, out var campo)) return null;
        if (!campo.TryGetProperty("Value", out var valor) || valor.ValueKind != JsonValueKind.Array || valor.GetArrayLength() == 0)
            return null;
        var primeiro = valor[0];
        return primeiro.ValueKind == JsonValueKind.String ? primeiro.GetString() : primeiro.ToString();
    }
}

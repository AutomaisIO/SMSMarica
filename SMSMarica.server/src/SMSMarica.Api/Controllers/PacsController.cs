using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Pacs;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Proxy transparente para o PACS dcm4chee. O catch-all encaminha tanto
/// chamadas QIDO-RS (JSON: estudos/séries/instâncias) quanto WADO-RS
/// (binário multipart das imagens), preservando status e Content-Type.
/// O front monta o <c>wadoRsRoot</c> apontando para <c>/pacs/rs</c>.
/// </summary>
[ApiController]
[Route("pacs")]
public sealed class PacsController(
    IPacsProxyService pacs,
    IPacsCache cache,
    IPacsTranscodeService transcode,
    IPacsWarmupService warmup,
    IServiceScopeFactory scopeFactory,
    ILogger<PacsController> logger) : ControllerBase
{
    /// <summary>
    /// Code+designator (DCM 113001 — "Rejected for Quality Reasons") usado para
    /// marcar o estudo como rejeitado antes do delete permanente. O dcm4chee só
    /// libera o DELETE depois que o estudo está em algum reject code, conforme
    /// <c>dcmAllowDeleteStudyPermanently = REJECTED</c> (ver docs/pacs.md §7).
    /// </summary>
    private const string RejectCodeQualidade = "113001%5EDCM";

    /// <summary>
    /// Header de cache "para sempre" usado em conteúdo IMUTÁVEL por instância
    /// (tudo sob <c>/instances/</c>: frames, bulk e metadata da instância). O pixel
    /// data de uma SOP Instance DICOM nunca muda, então o browser pode reusar a
    /// resposta sem revalidar.
    /// </summary>
    private const string CacheControlImutavel = "private, max-age=31536000, immutable";

    private readonly IPacsProxyService _pacs = pacs;
    private readonly IPacsCache _cache = cache;
    private readonly IPacsTranscodeService _transcode = transcode;
    private readonly IPacsWarmupService _warmup = warmup;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<PacsController> _logger = logger;

    [HttpGet("rs/{**caminho}")]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Consulta)]
    public async Task EncaminharRs(string caminho, CancellationToken cancellationToken)
    {
        var queryString = Request.QueryString.Value ?? string.Empty;
        var accept = Request.Headers.Accept.ToString();
        var imutavel = EhCaminhoImutavel(caminho);

        // Parâmetros INTERNOS do visualizador que entram na chave de cache mas NÃO
        // podem ir ao dcm4chee (ele não os conhece):
        //  - ?ev=N        versão do encoding, versiona o cache imutável do browser;
        //  - ?semCompressao=1  pede o frame CRU (desvia do transcode) ao "recriar".
        var semCompressao = PedeSemCompressao(queryString);
        var queryUpstream = RemoverParamsInternos(queryString);

        // (C) Compressão JPEG-LS Lossless (flag Pacs:Compressao:Habilitado, default false).
        // Só para requisições de frame; serve a variante comprimida (cache-first, chave
        // DISTINTA da do frame cru). Qualquer falha de transcode cai no caminho atual
        // (frame cru ~53MB) sem nunca quebrar a visualização.
        if (_transcode.Habilitado && !string.IsNullOrEmpty(caminho)
            && caminho.Contains("/frames/", StringComparison.Ordinal)
            && !semCompressao)
        {
            if (await ServirFrameComprimidoAsync(caminho, queryString, cancellationToken))
            {
                return;
            }
            // Falhou (transcode null/exceção): segue para o passthrough cru abaixo.
        }

        // (B) Cache em disco SOMENTE para conteúdo imutável por instância e método GET.
        if (imutavel && _cache.Habilitado)
        {
            var chave = _cache.CalcularChave("GET", caminho, queryString);

            // Cache hit: serve do disco com o content-type guardado + header imutável.
            if (_cache.TryGet(chave, out var ctCache, out var bytesCache))
            {
                Response.StatusCode = (int)HttpStatusCode.OK;
                Response.ContentType = ctCache;
                Response.Headers.CacheControl = CacheControlImutavel;
                await Response.Body.WriteAsync(bytesCache, cancellationToken);
                return;
            }

            using var resposta = await _pacs.EncaminharAsync(
                HttpMethod.Get, caminho, queryUpstream, accept, cancellationToken);

            Response.StatusCode = (int)resposta.StatusCode;
            if (resposta.Content.Headers.ContentType is not null)
            {
                Response.ContentType = resposta.Content.Headers.ContentType.ToString();
            }

            if (resposta.IsSuccessStatusCode)
            {
                Response.Headers.CacheControl = CacheControlImutavel; // (A)

                // Bufferiza com teto RÍGIDO (sem depender de Content-Length, que o
                // dcm4chee não manda no multipart chunked). Cabendo no teto, cacheia;
                // estourando, repassa o restante por streaming sem cachear.
                var teto = _cache.TetoItemBytes > 0 ? _cache.TetoItemBytes : StreamLimitado.TetoSegurancaPadrao;
                await using var origem = await resposta.Content.ReadAsStreamAsync(cancellationToken);
                var (buffer, completo) = await StreamLimitado.LerComTetoAsync(origem, teto, cancellationToken);

                await Response.Body.WriteAsync(buffer, cancellationToken);
                if (completo)
                {
                    _cache.Set(chave, Response.ContentType ?? "application/octet-stream", buffer);
                }
                else
                {
                    await origem.CopyToAsync(Response.Body, cancellationToken);
                }
                return;
            }

            // Não-2xx: repassa por streaming, sem cachear nem marcar como imutável.
            await using var erroStream = await resposta.Content.ReadAsStreamAsync(cancellationToken);
            await erroStream.CopyToAsync(Response.Body, cancellationToken);
            return;
        }

        // Caminho NÃO-imutável (QIDO/listagens) ou cache desligado: streaming como hoje.
        using var upstream = await _pacs.EncaminharAsync(
            HttpMethod.Get, caminho, queryUpstream, accept, cancellationToken);

        Response.StatusCode = (int)upstream.StatusCode;
        if (upstream.Content.Headers.ContentType is not null)
        {
            // Preserva o Content-Type completo (inclui boundary do multipart/related no WADO-RS).
            Response.ContentType = upstream.Content.Headers.ContentType.ToString();
        }

        // (A) Mesmo sem cache em disco, marca conteúdo imutável como cacheável no browser.
        if (imutavel && upstream.IsSuccessStatusCode)
        {
            Response.Headers.CacheControl = CacheControlImutavel;
        }

        await using var stream = await upstream.Content.ReadAsStreamAsync(cancellationToken);
        await stream.CopyToAsync(Response.Body, cancellationToken);
    }

    /// <summary>
    /// Serve a variante JPEG-LS Lossless de um frame (cache-first). Retorna
    /// <c>true</c> se respondeu (hit do cache ou transcode bem-sucedido); <c>false</c>
    /// se o transcode não produziu bytes — nesse caso o chamador faz passthrough cru.
    /// A chave de cache é DISTINTA da do frame cru (sufixo via DiscriminarCaminho)
    /// para nunca servir/sobrescrever os bytes errados sob a mesma URL imutável.
    /// </summary>
    private async Task<bool> ServirFrameComprimidoAsync(
        string caminho, string queryString, CancellationToken cancellationToken)
    {
        var chave = _cache.CalcularChave("GET", _transcode.DiscriminarCaminho(caminho), queryString);

        // Cache hit: serve do disco o envelope multipart comprimido já pronto.
        if (_cache.Habilitado && _cache.TryGet(chave, out var ctCache, out var bytesCache))
        {
            Response.StatusCode = (int)HttpStatusCode.OK;
            Response.ContentType = ctCache;
            Response.Headers.CacheControl = CacheControlImutavel;
            await Response.Body.WriteAsync(bytesCache, cancellationToken);
            return true;
        }

        // Miss: transcoda agora. Null = falha → fallback para o frame cru.
        var comprimido = await _transcode.TranscodificarFrameAsync(caminho, cancellationToken);
        if (comprimido is null) return false;

        if (_cache.Habilitado)
        {
            _cache.Set(chave, comprimido.ContentType, comprimido.Conteudo);
        }

        Response.StatusCode = (int)HttpStatusCode.OK;
        Response.ContentType = comprimido.ContentType;
        Response.Headers.CacheControl = CacheControlImutavel;
        await Response.Body.WriteAsync(comprimido.Conteudo, cancellationToken);
        return true;
    }

    /// <summary>
    /// Pré-aquece o cache de imagens de um estudo (item de performance): responde
    /// 202 imediatamente e dispara, em background, o download dos frames para o
    /// cache local, deixando a primeira visualização do médico instantânea.
    /// </summary>
    [HttpPost("aquecer/{studyUid}")]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult Aquecer(string studyUid)
    {
        // Escopo próprio: o request HTTP termina já-já (202), mas o aquecimento
        // continua. Capturamos e logamos qualquer exceção para nunca derrubar o host.
        _ = Task.Run(async () =>
        {
            try
            {
                using var escopo = _scopeFactory.CreateScope();
                var warmup = escopo.ServiceProvider.GetRequiredService<IPacsWarmupService>();
                await warmup.AquecerEstudoAsync(studyUid, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha no pré-aquecimento do estudo {StudyUid}.", studyUid);
            }
        });

        return Accepted();
    }

    /// <summary>
    /// Descarta o cache local de UMA imagem (instância) e o reconstrói. Corrige o
    /// caso em que a entrada em cache de um frame ficou corrompida (ex.: imagem
    /// diagnóstica abre em branco) enquanto a thumbnail — outra entrada — está OK,
    /// e como o conteúdo é servido como imutável não dá pra "re-pedir" pelo fluxo
    /// normal. Invalida TODAS as variantes que o visualizador gera para a instância
    /// (frame cru, frame JPEG-LS, thumbnail 160 e preview 1024) e, em seguida,
    /// re-aquece o frame no servidor para a próxima visualização já cair em cache hit.
    /// </summary>
    [HttpPost("cache/recriar/{studyUid}/{seriesUid}/{sopUid}")]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecriarImagensDaInstancia(
        string studyUid, string seriesUid, string sopUid, CancellationToken cancellationToken)
    {
        var baseInstancia = $"studies/{studyUid}/series/{seriesUid}/instances/{sopUid}";
        var frame = $"{baseInstancia}/frames/1";
        var rendered = $"{baseInstancia}/rendered";

        // As chaves batem com as que o GET /pacs/rs calcula (mesmo método/caminho/
        // queryString e o mesmo discriminante de transfer-syntax). A queryString do
        // /rendered inclui o "?" (idêntico ao Request.QueryString.Value do proxy).
        string[] chaves =
        [
            _cache.CalcularChave("GET", frame, string.Empty),                             // frame cru
            _cache.CalcularChave("GET", _transcode.DiscriminarCaminho(frame), string.Empty), // frame JPEG-LS
            _cache.CalcularChave("GET", frame, "?" + SentinelaSemCompressao),             // frame cru (sentinela)
            _cache.CalcularChave("GET", rendered, "?viewport=160,160"),                   // thumbnail
            _cache.CalcularChave("GET", rendered, "?viewport=1024,1024"),                 // preview
        ];
        foreach (var chave in chaves) _cache.Invalidar(chave);

        _logger.LogInformation(
            "Cache PACS recriado para a instância {SopUid} (estudo {StudyUid}).", sopUid, studyUid);

        // Re-aquece a variante CRUA (sentinela) — é para ela que o visualizador
        // recarrega ao recriar, desviando do transcode que produziu o frame ilegível.
        // Síncrono e best-effort: falha aqui não impede o front de re-pedir do PACS.
        await _warmup.AquecerInstanciaAsync(
            studyUid, seriesUid, sopUid, semCompressao: true, cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Conteúdo IMUTÁVEL por instância: tudo sob <c>/instances/</c> (a instância
    /// individual, seus <c>/frames/</c> e seu <c>/metadata</c> — no WADO-RS frames
    /// e metadata de instância sempre ficam sob <c>/instances/</c>).
    /// Ficam de fora (podem mudar e NÃO entram aqui): listagens/buscas QIDO
    /// (<c>.../studies</c>, <c>.../series</c>) e a metadata AGREGADA de estudo/série
    /// (<c>studies/{u}/metadata</c>, <c>.../series/{u}/metadata</c>), que crescem
    /// quando novas imagens chegam ao estudo.
    /// </summary>
    private static bool EhCaminhoImutavel(string caminho)
        => !string.IsNullOrEmpty(caminho)
            && caminho.Contains("/instances/", StringComparison.Ordinal);

    /// <summary>
    /// Sentinela do visualizador (<c>?semCompressao=1</c>) que pede o frame CRU,
    /// desviando do transcode JPEG-LS. Ver <see cref="EncaminharRs"/>.
    /// </summary>
    private const string SentinelaSemCompressao = "semCompressao=1";

    private static bool PedeSemCompressao(string queryString)
        => queryString.Contains(SentinelaSemCompressao, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Remove os parâmetros INTERNOS do visualizador (<c>ev</c>, <c>semCompressao</c>)
    /// de uma queryString antes de encaminhá-la ao dcm4chee, preservando os demais
    /// (ex.: <c>viewport</c> do /rendered). Devolve começando por <c>?</c> ou vazio.
    /// </summary>
    private static string RemoverParamsInternos(string queryString)
    {
        if (string.IsNullOrEmpty(queryString)) return string.Empty;
        var q = queryString.StartsWith('?') ? queryString[1..] : queryString;
        var mantidos = q.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(par =>
            {
                var chave = par.Split('=', 2)[0];
                return !chave.Equals("ev", StringComparison.OrdinalIgnoreCase)
                    && !chave.Equals("semCompressao", StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
        return mantidos.Length == 0 ? string.Empty : "?" + string.Join('&', mantidos);
    }

    /// <summary>
    /// Exclui um estudo do PACS. O dcm4chee exige duas operações: primeiro
    /// rejeitar (POST .../reject/{code}) e depois o DELETE permanente. Aqui
    /// embrulhamos o passo-a-passo num endpoint simples para o front.
    /// 409 Conflict no reject (estudo já rejeitado) é tratado como sucesso e
    /// o fluxo segue para o DELETE.
    /// </summary>
    [HttpDelete("rs/studies/{studyUid}")]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExcluirEstudo(string studyUid, CancellationToken cancellationToken)
    {
        using (var rejeicao = await _pacs.EncaminharAsync(
            HttpMethod.Post,
            $"studies/{studyUid}/reject/{RejectCodeQualidade}",
            queryString: string.Empty,
            accept: null,
            cancellationToken))
        {
            // 2xx ou 409 (já rejeitado) seguem para o DELETE; outros viram falha.
            if (!rejeicao.IsSuccessStatusCode && rejeicao.StatusCode != HttpStatusCode.Conflict)
            {
                return StatusCode((int)rejeicao.StatusCode);
            }
        }

        using var delete = await _pacs.EncaminharAsync(
            HttpMethod.Delete,
            $"studies/{studyUid}",
            queryString: string.Empty,
            accept: null,
            cancellationToken);

        return StatusCode((int)delete.StatusCode);
    }
}

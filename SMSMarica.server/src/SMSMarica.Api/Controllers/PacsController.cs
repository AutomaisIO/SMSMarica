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
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<PacsController> _logger = logger;

    [HttpGet("rs/{**caminho}")]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Consulta)]
    public async Task EncaminharRs(string caminho, CancellationToken cancellationToken)
    {
        var queryString = Request.QueryString.Value ?? string.Empty;
        var accept = Request.Headers.Accept.ToString();
        var imutavel = EhCaminhoImutavel(caminho);

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
                HttpMethod.Get, caminho, queryString, accept, cancellationToken);

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
            HttpMethod.Get, caminho, queryString, accept, cancellationToken);

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

using System.Net;
using Microsoft.AspNetCore.Mvc;
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
public sealed class PacsController(IPacsProxyService pacs) : ControllerBase
{
    /// <summary>
    /// Code+designator (DCM 113001 — "Rejected for Quality Reasons") usado para
    /// marcar o estudo como rejeitado antes do delete permanente. O dcm4chee só
    /// libera o DELETE depois que o estudo está em algum reject code, conforme
    /// <c>dcmAllowDeleteStudyPermanently = REJECTED</c> (ver docs/pacs.md §7).
    /// </summary>
    private const string RejectCodeQualidade = "113001%5EDCM";

    private readonly IPacsProxyService _pacs = pacs;

    [HttpGet("rs/{**caminho}")]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Consulta)]
    public async Task EncaminharRs(string caminho, CancellationToken cancellationToken)
    {
        var queryString = Request.QueryString.Value ?? string.Empty;
        var accept = Request.Headers.Accept.ToString();

        using var upstream = await _pacs.EncaminharAsync(
            HttpMethod.Get, caminho, queryString, accept, cancellationToken);

        Response.StatusCode = (int)upstream.StatusCode;
        if (upstream.Content.Headers.ContentType is not null)
        {
            // Preserva o Content-Type completo (inclui boundary do multipart/related no WADO-RS).
            Response.ContentType = upstream.Content.Headers.ContentType.ToString();
        }

        await using var stream = await upstream.Content.ReadAsStreamAsync(cancellationToken);
        await stream.CopyToAsync(Response.Body, cancellationToken);
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

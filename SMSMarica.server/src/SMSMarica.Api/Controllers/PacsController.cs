using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Pacs;

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
    private readonly IPacsProxyService _pacs = pacs;

    [HttpGet("rs/{**caminho}")]
    public async Task EncaminharRs(string caminho, CancellationToken cancellationToken)
    {
        var queryString = Request.QueryString.Value ?? string.Empty;
        var accept = Request.Headers.Accept.ToString();

        using var upstream = await _pacs.EncaminharAsync(caminho, queryString, accept, cancellationToken);

        Response.StatusCode = (int)upstream.StatusCode;
        if (upstream.Content.Headers.ContentType is not null)
        {
            // Preserva o Content-Type completo (inclui boundary do multipart/related no WADO-RS).
            Response.ContentType = upstream.Content.Headers.ContentType.ToString();
        }

        await using var stream = await upstream.Content.ReadAsStreamAsync(cancellationToken);
        await stream.CopyToAsync(Response.Body, cancellationToken);
    }
}

using Automais.Pabx.Api.Cdr;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Pabx.Api.Controllers;

[ApiController]
[Route("api/cdr")]
public sealed class CdrController(ICdrService cdrService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CdrPaginaDto>> Listar(
        [FromQuery] string? ramal,
        [FromQuery] string? numero,
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 50,
        CancellationToken ct = default)
    {
        if (!cdrService.Configurado)
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "CDR não configurado",
                detail: "Defina ConnectionStrings:CdrDb (MySQL local do Asterisk) na configuração do serviço.");

        return await cdrService.ListarAsync(new CdrFiltro(ramal, numero, de, ate, pagina, tamanhoPagina), ct);
    }
}

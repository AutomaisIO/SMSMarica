using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Geo;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("geo")]
public sealed class GeoController(IGeocodificadorService service) : ControllerBase
{
    private readonly IGeocodificadorService _service = service;

    /// <summary>Endereços que não geocodificaram com confiança — fila de revisão (pin manual).</summary>
    [HttpGet("revisao")]
    [RequerPermissao(ModuloPermissao.Unidades, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<GeocodigoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<GeocodigoDto>> Revisao(CancellationToken cancellationToken) =>
        await _service.ListarRevisaoPendenteAsync(cancellationToken);

    /// <summary>Fixa manualmente a coordenada (pin no mapa) de um geocódigo pendente.</summary>
    [HttpPost("fixar")]
    [RequerPermissao(ModuloPermissao.Unidades, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Fixar([FromBody] FixarGeocodigoRequest request, CancellationToken cancellationToken)
    {
        await _service.FixarManualAsync(request, cancellationToken);
        return NoContent();
    }
}

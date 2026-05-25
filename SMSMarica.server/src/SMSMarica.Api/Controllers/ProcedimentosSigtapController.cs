using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Procedimentos;
using SMSMarica.Core.Procedimentos.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Catálogo SIGTAP (DataSUS) — read-only. Atualizações vêm de seed/import
/// administrativo (CSV oficial). Usado para amarrar TipoExame ao código oficial.
/// </summary>
[ApiController]
[Route("procedimentos-sigtap")]
public sealed class ProcedimentosSigtapController(IProcedimentosSigtapService service) : ControllerBase
{
    private readonly IProcedimentosSigtapService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.ProcedimentosSigtap, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ProcedimentoSigtapDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ProcedimentoSigtapDto>> Listar(
        [FromQuery] string? busca,
        [FromQuery] string? grupo,
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default) =>
        await _service.ListarAsync(busca, grupo, limite, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.ProcedimentosSigtap, AcoesPermissao.Consulta)]
    [ProducesResponseType<ProcedimentoSigtapDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ProcedimentoSigtapDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);
}

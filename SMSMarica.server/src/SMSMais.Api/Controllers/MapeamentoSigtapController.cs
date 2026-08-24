using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Mapeamento;
using SMSMais.Core.Mapeamento.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Workspace de mapeamento SIGTAP→TipoExame: exames de imagem importados sem tipo (pendentes).
/// Vincular um TipoExame ao código SIGTAP faz o backfill de todos os exames que casam. Ver ADR-0021.
/// </summary>
[ApiController]
[Route("mapeamento-sigtap")]
public sealed class MapeamentoSigtapController(IMapeamentoSigtapService service) : ControllerBase
{
    [HttpGet("pendentes")]
    [RequerPermissao(ModuloPermissao.MapeamentoSigtap, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<PendenteMapeamentoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<PendenteMapeamentoDto>> Pendentes(CancellationToken cancellationToken)
        => service.ListarPendentesAsync(cancellationToken);

    [HttpPost("vincular")]
    [RequerPermissao(ModuloPermissao.MapeamentoSigtap, AcoesPermissao.Edicao)]
    [ProducesResponseType<VincularMapeamentoResultado>(StatusCodes.Status200OK)]
    public Task<VincularMapeamentoResultado> Vincular(
        [FromBody] VincularMapeamentoRequest request, CancellationToken cancellationToken)
        => service.VincularAsync(request, cancellationToken);
}

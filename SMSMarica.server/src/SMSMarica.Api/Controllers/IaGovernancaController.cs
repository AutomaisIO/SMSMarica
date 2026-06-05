using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Inteligencia.Governanca;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Governança do aprendizado do módulo IA: revisar/desativar instruções aprendidas e
/// auditar o histórico de correções automáticas de SQL.
/// </summary>
[ApiController]
[Route("ia")]
public sealed class IaGovernancaController(IIaGovernancaService service) : ControllerBase
{
    private readonly IIaGovernancaService _service = service;

    [HttpGet("aprendizados")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AprendizadoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AprendizadoDto>> ListarAprendizados(
        [FromQuery] Guid? fonteId,
        CancellationToken cancellationToken) =>
        await _service.ListarAprendizadosAsync(fonteId, cancellationToken);

    [HttpDelete("aprendizados/{id:guid}")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesativarAprendizado(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAprendizadoAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("correcoes")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<CorrecaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<CorrecaoDto>> ListarCorrecoes(
        [FromQuery] Guid? fonteId,
        CancellationToken cancellationToken) =>
        await _service.ListarCorrecoesAsync(fonteId, cancellationToken);
}

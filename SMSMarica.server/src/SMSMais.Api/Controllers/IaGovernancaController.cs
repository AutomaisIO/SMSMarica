using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Inteligencia.Governanca;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

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
    [RequerPermissao(ModuloPermissao.InteligenciaAprendizado, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AprendizadoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AprendizadoDto>> ListarAprendizados(
        [FromQuery] Guid? fonteId,
        CancellationToken cancellationToken) =>
        await _service.ListarAprendizadosAsync(fonteId, cancellationToken);

    [HttpDelete("aprendizados/{id:guid}")]
    [RequerPermissao(ModuloPermissao.InteligenciaAprendizado, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesativarAprendizado(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAprendizadoAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("correcoes")]
    [RequerPermissao(ModuloPermissao.InteligenciaAprendizado, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<CorrecaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<CorrecaoDto>> ListarCorrecoes(
        [FromQuery] Guid? fonteId,
        CancellationToken cancellationToken) =>
        await _service.ListarCorrecoesAsync(fonteId, cancellationToken);

    /// <summary>Avaliações 👎 da Consulta Inteligente pendentes de tratamento.</summary>
    [HttpGet("feedbacks")]
    [RequerPermissao(ModuloPermissao.InteligenciaAprendizado, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<FeedbackDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<FeedbackDto>> ListarFeedbacks(
        [FromQuery] bool apenasPendentes = true,
        CancellationToken cancellationToken = default) =>
        await _service.ListarFeedbacksAsync(apenasPendentes, cancellationToken);

    [HttpPost("feedbacks/{id:guid}/tratar")]
    [RequerPermissao(ModuloPermissao.InteligenciaAprendizado, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TratarFeedback(
        Guid id, [FromBody] TratarFeedbackRequest request, CancellationToken cancellationToken)
    {
        await _service.TratarFeedbackAsync(id, request, cancellationToken);
        return NoContent();
    }
}

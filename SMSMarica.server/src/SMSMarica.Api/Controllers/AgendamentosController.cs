using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Agendamentos;
using SMSMarica.Core.Agendamentos.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Horários livres de uma agenda e ciclo de vida do agendamento (marcar → confirmar →
/// realizar / faltar / cancelar). Ver ADR-0012.
/// </summary>
[ApiController]
[Route("agendamentos")]
public sealed class AgendamentosController(IAgendamentoService service) : ControllerBase
{
    private readonly IAgendamentoService _service = service;

    /// <summary>Horários livres de uma agenda no intervalo informado.</summary>
    [HttpGet("agendas/{agendaId:guid}/horarios-livres")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SlotLivreDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<SlotLivreDto>> HorariosLivres(
        Guid agendaId,
        [FromQuery] DateOnly inicio,
        [FromQuery] DateOnly fim,
        CancellationToken cancellationToken) =>
        await _service.CalcularHorariosLivresAsync(agendaId, inicio, fim, cancellationToken);

    /// <summary>Agendamentos de uma agenda no intervalo informado.</summary>
    [HttpGet("agendas/{agendaId:guid}")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AgendamentoListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AgendamentoListItemDto>> ListarPorAgenda(
        Guid agendaId,
        [FromQuery] DateOnly inicio,
        [FromQuery] DateOnly fim,
        CancellationToken cancellationToken) =>
        await _service.ListarAsync(agendaId, inicio, fim, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<AgendamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AgendamentoDto> Obter(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Agendar(
        [FromBody] AgendarRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.AgendarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id }, id);
    }

    [HttpPost("{id:guid}/confirmar")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirmar(Guid id, CancellationToken cancellationToken)
    {
        await _service.ConfirmarAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/realizar")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Realizar(Guid id, CancellationToken cancellationToken)
    {
        await _service.RealizarAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/falta")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegistrarFalta(Guid id, CancellationToken cancellationToken)
    {
        await _service.RegistrarFaltaAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancelar")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(
        Guid id,
        [FromBody] CancelarAgendamentoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.CancelarAsync(id, request, cancellationToken);
        return NoContent();
    }
}

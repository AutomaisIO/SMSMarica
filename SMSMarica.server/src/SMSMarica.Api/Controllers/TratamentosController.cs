using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Tratamentos;
using SMSMarica.Core.Tratamentos.Dtos;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("tratamentos")]
public sealed class TratamentosController(ITratamentosService service) : ControllerBase
{
    private readonly ITratamentosService _service = service;

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TratamentoListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<TratamentoListItemDto>> Listar(
        [FromQuery] Guid? pacienteId,
        CancellationToken cancellationToken) =>
        pacienteId is { } id
            ? await _service.ListarPorPacienteAsync(id, cancellationToken)
            : await _service.ListarAsync(cancellationToken);

    [HttpGet("tipos")]
    [ProducesResponseType<IReadOnlyList<TipoTratamentoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<TipoTratamentoDto>> ListarTipos(CancellationToken cancellationToken) =>
        await _service.ListarTiposAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TratamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<TratamentoDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    /// <summary>
    /// Expande uma regra de periodicidade em uma lista de datas — usado
    /// pela prévia do wizard de cadastro, antes de persistir.
    /// </summary>
    [HttpPost("periodicidade/expandir")]
    [ProducesResponseType<IReadOnlyList<DateOnly>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DateOnly>> Expandir([FromBody] ExpandirPeriodicidadeRequest request) =>
        Ok(_service.ExpandirPeriodicidade(request));

    [HttpPost]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarTratamentoRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarTratamentoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Encerrar(Guid id, CancellationToken cancellationToken)
    {
        await _service.EncerrarAsync(id, cancellationToken);
        return NoContent();
    }

    // ---- Sessões

    [HttpPost("{id:guid}/sessoes")]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdicionarSessao(
        Guid id,
        [FromBody] AdicionarSessaoRequest request,
        CancellationToken cancellationToken)
    {
        var sessaoId = await _service.AdicionarSessaoAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, sessaoId);
    }

    [HttpPut("{id:guid}/sessoes/{sessaoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AtualizarSessao(
        Guid id,
        Guid sessaoId,
        [FromBody] AtualizarSessaoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarSessaoAsync(id, sessaoId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/sessoes/{sessaoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarSessao(
        Guid id,
        Guid sessaoId,
        CancellationToken cancellationToken)
    {
        await _service.CancelarSessaoAsync(id, sessaoId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Confirma realização (ou não realização) de uma sessão — registra
    /// acompanhante, horários, motorista e veículo de ida/volta.
    /// </summary>
    [HttpPost("{id:guid}/sessoes/{sessaoId:guid}/confirmar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmarSessao(
        Guid id,
        Guid sessaoId,
        [FromBody] ConfirmarSessaoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.ConfirmarSessaoAsync(id, sessaoId, request, cancellationToken);
        return NoContent();
    }
}

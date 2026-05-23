using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Motoristas;
using SMSMarica.Core.Motoristas.Dtos;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("motoristas")]
public sealed class MotoristasController(IMotoristasService service) : ControllerBase
{
    private readonly IMotoristasService _service = service;

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MotoristaListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<MotoristaListItemDto>> Listar(CancellationToken cancellationToken) =>
        await _service.ListarAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MotoristaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<MotoristaDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarMotoristaRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPost("promover")]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Promover(
        [FromBody] PromoverMotoristaRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.PromoverAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarMotoristaRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }
}

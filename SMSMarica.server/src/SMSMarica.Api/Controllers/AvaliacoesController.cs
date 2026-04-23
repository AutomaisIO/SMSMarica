using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Avaliacoes;
using SMSMarica.Core.Avaliacoes.Dtos;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("avaliacoes")]
public sealed class AvaliacoesController(IAvaliacoesService service) : ControllerBase
{
    private readonly IAvaliacoesService _service = service;

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AvaliacaoListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AvaliacaoListItemDto>> Listar(CancellationToken cancellationToken) =>
        await _service.ListarAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AvaliacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AvaliacaoDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarAvaliacaoRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.RegistrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarAvaliacaoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deletar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeletarAsync(id, cancellationToken);
        return NoContent();
    }
}

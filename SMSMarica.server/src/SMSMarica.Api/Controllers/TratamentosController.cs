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
    public async Task<IReadOnlyList<TratamentoListItemDto>> Listar(CancellationToken cancellationToken) =>
        await _service.ListarAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TratamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<TratamentoDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

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
}

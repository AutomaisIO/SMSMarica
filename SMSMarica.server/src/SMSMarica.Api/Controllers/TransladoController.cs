using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Translado;
using SMSMarica.Core.Translado.Dtos;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("rotas")]
public sealed class TransladoController(ITransladoService service) : ControllerBase
{
    private readonly ITransladoService _service = service;

    /// <summary>Lista rotas. Parâmetro opcional <c>data</c> filtra por dia (YYYY-MM-DD).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RotaDiariaListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<RotaDiariaListItemDto>> Listar(
        [FromQuery] DateOnly? data,
        CancellationToken cancellationToken) =>
        await _service.ListarAsync(data, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RotaDiariaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RotaDiariaDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarRotaRequest request,
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
        [FromBody] AtualizarRotaRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/iniciar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Iniciar(Guid id, CancellationToken cancellationToken)
    {
        await _service.IniciarAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/concluir")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Concluir(Guid id, CancellationToken cancellationToken)
    {
        await _service.ConcluirAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken cancellationToken)
    {
        await _service.CancelarAsync(id, cancellationToken);
        return NoContent();
    }
}

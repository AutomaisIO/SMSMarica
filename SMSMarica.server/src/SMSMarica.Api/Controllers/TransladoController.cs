using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Translado;
using SMSMarica.Core.Translado.Dtos;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("rotas")]
public sealed class TransladoController(ITransladoService service) : ControllerBase
{
    private readonly ITransladoService _service = service;

    /// <summary>
    /// Lista rotas. Filtros opcionais: <c>data</c> (YYYY-MM-DD), <c>motoristaId</c>, <c>veiculoId</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RotaDiariaListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<RotaDiariaListItemDto>> Listar(
        [FromQuery] DateOnly? data,
        [FromQuery] Guid? motoristaId,
        [FromQuery] Guid? veiculoId,
        CancellationToken cancellationToken) =>
        await _service.ListarAsync(data, motoristaId, veiculoId, cancellationToken);

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

    /// <summary>
    /// Lista sessões elegíveis para alocação nesta rota: pendentes/confirmadas
    /// cuja DataPrevista é ≤ data da rota e que ainda não foram alocadas em
    /// nenhuma outra rota ativa.
    /// </summary>
    [HttpGet("{id:guid}/sessoes-elegiveis")]
    [ProducesResponseType<IReadOnlyList<SessaoElegivelDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<SessaoElegivelDto>> ListarSessoesElegiveis(
        Guid id, CancellationToken cancellationToken) =>
        await _service.ListarSessoesElegiveisAsync(id, cancellationToken);

    [HttpPost("{id:guid}/alocacoes")]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarAlocacao(
        Guid id,
        [FromBody] CriarAlocacaoRequest request,
        CancellationToken cancellationToken)
    {
        var alocacaoId = await _service.CriarAlocacaoAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, alocacaoId);
    }

    [HttpDelete("{id:guid}/alocacoes/{alocacaoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoverAlocacao(
        Guid id, Guid alocacaoId, CancellationToken cancellationToken)
    {
        await _service.RemoverAlocacaoAsync(id, alocacaoId, cancellationToken);
        return NoContent();
    }
}

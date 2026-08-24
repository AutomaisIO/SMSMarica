using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Veiculos;
using SMSMais.Core.Veiculos.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

[ApiController]
[Route("veiculos")]
public sealed class VeiculosController(IVeiculosService service) : ControllerBase
{
    private readonly IVeiculosService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.Veiculos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<VeiculoListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<VeiculoListItemDto>> Listar(CancellationToken cancellationToken) =>
        await _service.ListarAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Veiculos, AcoesPermissao.Consulta)]
    [ProducesResponseType<VeiculoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<VeiculoDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Veiculos, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarVeiculoRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Veiculos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarVeiculoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Veiculos, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/layout")]
    [RequerPermissao(ModuloPermissao.Veiculos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarLayout(
        Guid id,
        [FromBody] AtualizarLayoutVeiculoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarLayoutAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/fileiras")]
    [RequerPermissao(ModuloPermissao.Veiculos, AcoesPermissao.Edicao)]
    [ProducesResponseType<FileiraDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdicionarFileira(
        Guid id,
        [FromBody] AdicionarFileiraRequest request,
        CancellationToken cancellationToken)
    {
        var fileira = await _service.AdicionarFileiraAsync(id, request, cancellationToken);
        return Created($"/veiculos/{id}/fileiras/{fileira.Id}", fileira);
    }

    [HttpDelete("{id:guid}/fileiras/{fileiraId:guid}")]
    [RequerPermissao(ModuloPermissao.Veiculos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoverFileira(
        Guid id,
        Guid fileiraId,
        CancellationToken cancellationToken)
    {
        await _service.RemoverFileiraAsync(id, fileiraId, cancellationToken);
        return NoContent();
    }
}

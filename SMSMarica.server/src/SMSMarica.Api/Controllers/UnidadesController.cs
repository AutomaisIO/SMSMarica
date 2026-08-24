using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Unidades;
using SMSMais.Core.Unidades.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("unidades")]
public sealed class UnidadesController(IUnidadesService service) : ControllerBase
{
    private readonly IUnidadesService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.Unidades, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<UnidadeListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UnidadeListItemDto>> Listar(CancellationToken cancellationToken) =>
        await _service.ListarAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Unidades, AcoesPermissao.Consulta)]
    [ProducesResponseType<UnidadeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<UnidadeDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Unidades, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarUnidadeRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Unidades, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarUnidadeRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Unidades, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }
}

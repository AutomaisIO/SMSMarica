using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Perfis;
using SMSMais.Core.Perfis.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("perfis")]
public sealed class PerfisController(IPerfisService service) : ControllerBase
{
    private readonly IPerfisService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.Perfis, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<PerfilListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PerfilListItemDto>> Listar(CancellationToken cancellationToken) =>
        await _service.ListarAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Perfis, AcoesPermissao.Consulta)]
    [ProducesResponseType<PerfilDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<PerfilDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Perfis, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarPerfilRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Perfis, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarPerfilRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Perfis, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }
}

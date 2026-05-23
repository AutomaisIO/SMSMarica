using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Identidade.Dtos;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("usuarios")]
public sealed class IdentidadeController(IIdentidadeService service) : ControllerBase
{
    private readonly IIdentidadeService _service = service;

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UsuarioListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UsuarioListItemDto>> Listar(CancellationToken cancellationToken) =>
        await _service.ListarAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<UsuarioDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarUsuarioRequest request,
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

    [HttpGet("{id:guid}/permissoes")]
    [ProducesResponseType<PermissoesResolvidasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<PermissoesResolvidasDto> ObterPermissoes(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPermissoesResolvidasAsync(id, cancellationToken);

    [HttpPut("{id:guid}/perfis")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarPerfis(
        Guid id,
        [FromBody] AtualizarPerfisDoUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarPerfisDoUsuarioAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/overrides")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarOverrides(
        Guid id,
        [FromBody] AtualizarOverridesDoUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarOverridesDoUsuarioAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/senha")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AlterarSenha(
        Guid id,
        [FromBody] AlterarSenhaRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AlterarSenhaAsync(id, request, cancellationToken);
        return NoContent();
    }
}

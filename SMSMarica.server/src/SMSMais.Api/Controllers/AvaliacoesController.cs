using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Avaliacoes;
using SMSMais.Core.Avaliacoes.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

[ApiController]
[Route("avaliacoes")]
public sealed class AvaliacoesController(IAvaliacoesService service) : ControllerBase
{
    private readonly IAvaliacoesService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.Avaliacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AvaliacaoListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AvaliacaoListItemDto>> Listar(CancellationToken cancellationToken) =>
        await _service.ListarAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Avaliacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<AvaliacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AvaliacaoDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Avaliacoes, AcoesPermissao.Inclusao)]
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
    [RequerPermissao(ModuloPermissao.Avaliacoes, AcoesPermissao.Edicao)]
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
    [RequerPermissao(ModuloPermissao.Avaliacoes, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deletar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeletarAsync(id, cancellationToken);
        return NoContent();
    }
}

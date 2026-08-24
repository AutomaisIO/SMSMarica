using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.TiposExame;
using SMSMais.Core.TiposExame.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// CRUD do catálogo curado de tipos de exame ofertados pela SMS (tradução
/// SUS → equipamento). Cada tipo aponta para um ProcedimentoSigtap.
/// </summary>
[ApiController]
[Route("tipos-exame")]
public sealed class TiposExameController(ITiposExameService service) : ControllerBase
{
    private readonly ITiposExameService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.TiposExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<TipoExameListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<TipoExameListItemDto>> Listar(
        [FromQuery] ModalidadeDicom? modalidade,
        [FromQuery] bool incluirInativos = false,
        CancellationToken cancellationToken = default) =>
        await _service.ListarAsync(modalidade, incluirInativos, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.TiposExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<TipoExameDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<TipoExameDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.TiposExame, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarTipoExameRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.TiposExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarTipoExameRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.TiposExame, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        await _service.ExcluirAsync(id, cancellationToken);
        return NoContent();
    }
}

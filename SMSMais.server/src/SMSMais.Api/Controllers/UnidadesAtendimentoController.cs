using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.UnidadesAtendimento;
using SMSMais.Core.UnidadesAtendimento.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Unidades de atendimento — destinos do Transporte de Pacientes (endereço + coordenada para a
/// rota). O seletor do cadastro de tratamento NÃO passa por aqui: usa
/// <c>GET /tratamentos/unidades-atendimento</c>, com a permissão de Tratamentos.
/// </summary>
[ApiController]
[Route("unidades-atendimento")]
public sealed class UnidadesAtendimentoController(IUnidadesAtendimentoService service) : ControllerBase
{
    private readonly IUnidadesAtendimentoService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.UnidadesAtendimento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<UnidadeAtendimentoListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UnidadeAtendimentoListItemDto>> Listar(
        [FromQuery] bool incluirInativas,
        CancellationToken cancellationToken) =>
        await _service.ListarAsync(incluirInativas, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.UnidadesAtendimento, AcoesPermissao.Consulta)]
    [ProducesResponseType<UnidadeAtendimentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<UnidadeAtendimentoDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.UnidadesAtendimento, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] SalvarUnidadeAtendimentoRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.UnidadesAtendimento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] SalvarUnidadeAtendimentoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.UnidadesAtendimento, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reativar")]
    [RequerPermissao(ModuloPermissao.UnidadesAtendimento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.ReativarAsync(id, cancellationToken);
        return NoContent();
    }
}

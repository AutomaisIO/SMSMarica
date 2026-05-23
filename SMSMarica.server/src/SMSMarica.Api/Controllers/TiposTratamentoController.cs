using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.TiposTratamento;
using SMSMarica.Core.TiposTratamento.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("tipos-tratamento")]
public sealed class TiposTratamentoController(ITiposTratamentoService service) : ControllerBase
{
    private readonly ITiposTratamentoService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.TiposTratamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<TipoTratamentoListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<TipoTratamentoListItemDto>> Listar(
        [FromQuery] bool somenteAtivos = false,
        CancellationToken cancellationToken = default) =>
        await _service.ListarAsync(somenteAtivos, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.TiposTratamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<TipoTratamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<TipoTratamentoDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.TiposTratamento, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarTipoTratamentoRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.TiposTratamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarTipoTratamentoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.TiposTratamento, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }
}

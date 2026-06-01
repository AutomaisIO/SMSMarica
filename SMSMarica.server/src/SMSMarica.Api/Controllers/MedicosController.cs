using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Medicos;
using SMSMarica.Core.Medicos.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("medicos")]
public sealed class MedicosController(IMedicosService service) : ControllerBase
{
    private readonly IMedicosService _service = service;

    /// <summary>
    /// Busca em tempo real por nome (qualquer parte) ou CPF. Sem <c>termo</c>
    /// retorna os 10 últimos cadastros (LastUpdated desc). Limite 10.
    /// <para>
    /// <c>conselho</c> filtra por sigla exata (ex.: <c>CRM</c> no menu Médicos);
    /// <c>conselhoExceto</c> exclui uma sigla (ex.: <c>CRM</c> no menu Profissionais).
    /// </para>
    /// </summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Medicos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<MedicoListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<MedicoListItemDto>> Buscar(
        [FromQuery] string? termo,
        [FromQuery] string? conselho,
        [FromQuery] string? conselhoExceto,
        CancellationToken cancellationToken) =>
        await _service.BuscarAsync(termo, conselho, conselhoExceto, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Medicos, AcoesPermissao.Consulta)]
    [ProducesResponseType<MedicoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<MedicoDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Medicos, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarMedicoRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPost("promover")]
    [RequerPermissao(ModuloPermissao.Medicos, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Promover(
        [FromBody] PromoverMedicoRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.PromoverAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Medicos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarMedicoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Medicos, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }
}

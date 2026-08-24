using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Especialidades;
using SMSMais.Core.Especialidades.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>CRUD das especialidades médicas (tabela de referência do agendamento). Ver ADR-0012.</summary>
[ApiController]
[Route("especialidades")]
public sealed class EspecialidadesController(IEspecialidadesService service) : ControllerBase
{
    private readonly IEspecialidadesService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.Especialidades, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<EspecialidadeListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<EspecialidadeListItemDto>> Listar(
        [FromQuery] bool incluirInativas,
        CancellationToken cancellationToken) =>
        await _service.ListarAsync(incluirInativas, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Especialidades, AcoesPermissao.Consulta)]
    [ProducesResponseType<EspecialidadeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<EspecialidadeDto> Obter(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Especialidades, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarEspecialidadeRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Especialidades, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarEspecialidadeRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Especialidades, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        await _service.ExcluirAsync(id, cancellationToken);
        return NoContent();
    }
}

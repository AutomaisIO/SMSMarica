using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Dtos;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("pacientes")]
public sealed class PacientesController(IPacientesService service) : ControllerBase
{
    private readonly IPacientesService _service = service;

    /// <summary>Lista todos os pacientes cadastrados.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PacienteListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PacienteListItemDto>> Listar(CancellationToken cancellationToken) =>
        await _service.ListarAsync(cancellationToken);

    /// <summary>Retorna um paciente pelo identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PacienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<PacienteDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    /// <summary>Cadastra um novo paciente.</summary>
    [HttpPost]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarPacienteRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    /// <summary>Atualiza dados de um paciente existente.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarPacienteRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Desativa um paciente (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }
}

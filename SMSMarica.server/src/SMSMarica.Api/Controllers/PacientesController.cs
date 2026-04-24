using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Dtos;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("pacientes")]
public sealed class PacientesController(IPacientesService service) : ControllerBase
{
    private readonly IPacientesService _service = service;

    /// <summary>
    /// Busca em tempo real por nome (qualquer parte, múltiplos tokens) ou CPF.
    /// Sem <c>termo</c> retorna lista vazia (a base é grande). Limite 20.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PacienteListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PacienteListItemDto>> Buscar(
        [FromQuery] string? termo,
        CancellationToken cancellationToken) =>
        await _service.BuscarAsync(termo, cancellationToken);

    /// <summary>Retorna um paciente pelo identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PacienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<PacienteDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    /// <summary>
    /// Verifica se há paciente com o CPF informado (inclusive desativado).
    /// 404 se não existe; 200 com o resumo (incluindo <c>ativo</c>) se existe.
    /// </summary>
    [HttpGet("por-cpf/{cpf}")]
    [ProducesResponseType<PacienteExistenciaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorCpf(string cpf, CancellationToken cancellationToken)
    {
        var resultado = await _service.ObterPorCpfAsync(cpf, cancellationToken);
        return resultado is null ? NotFound() : Ok(resultado);
    }

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

    /// <summary>Desativa um paciente (soft delete) — some das listagens.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Reativa um paciente desativado (após confirmação no fluxo de cadastro).</summary>
    [HttpPost("{id:guid}/reativar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.ReativarAsync(id, cancellationToken);
        return NoContent();
    }
}

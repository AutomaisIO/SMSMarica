using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Atendimentos;
using SMSMarica.Core.Atendimentos.Dtos;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("pacientes")]
public sealed class PacientesController(IPacientesService service, IAtendimentosService atendimentos) : ControllerBase
{
    private readonly IPacientesService _service = service;
    private readonly IAtendimentosService _atendimentos = atendimentos;

    /// <summary>
    /// Busca em tempo real por nome (qualquer parte, múltiplos tokens) ou CPF.
    /// Sem <c>termo</c> retorna lista vazia (a base é grande). Limite 20.
    /// </summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<PacienteListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PacienteListItemDto>> Buscar(
        [FromQuery] string? termo,
        CancellationToken cancellationToken) =>
        await _service.BuscarAsync(termo, cancellationToken);

    /// <summary>Retorna um paciente pelo identificador.</summary>
    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<PacienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<PacienteDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    /// <summary>
    /// Histórico clínico do paciente (atendimentos + diagnósticos) vindo do hub
    /// FHIR (Encounter/Condition), originado do Salux. Timeline, mais recente primeiro.
    /// </summary>
    [HttpGet("{id:guid}/atendimentos")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AtendimentoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AtendimentoDto>> Atendimentos(Guid id, CancellationToken cancellationToken) =>
        await _atendimentos.ObterPorPacienteAsync(id, cancellationToken);

    /// <summary>
    /// Verifica se há paciente com o CPF informado (inclusive desativado).
    /// 404 se não existe; 200 com o resumo (incluindo <c>ativo</c>) se existe.
    /// </summary>
    [HttpGet("por-cpf/{cpf}")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<PacienteExistenciaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorCpf(string cpf, CancellationToken cancellationToken)
    {
        var resultado = await _service.ObterPorCpfAsync(cpf, cancellationToken);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    /// <summary>Cadastra um novo paciente.</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Inclusao)]
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

    /// <summary>
    /// Promove um Usuario existente (sem papel) a Paciente — usado quando o
    /// fluxo de cadastro detectou que o CPF já existe como usuário.
    /// </summary>
    [HttpPost("promover")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Promover(
        [FromBody] PromoverPacienteRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.PromoverAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    /// <summary>Atualiza dados de um paciente existente.</summary>
    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Edicao)]
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
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Exclusao)]
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
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.ReativarAsync(id, cancellationToken);
        return NoContent();
    }
}

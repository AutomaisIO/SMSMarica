using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Acompanhantes;
using SMSMais.Core.Acompanhantes.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Acompanhantes do paciente no Transporte de Pacientes, pelo painel. A lista é do paciente e vale
/// para todos os atendimentos dele. Fica sob a permissão de Tratamentos (Atendimentos): quem cuida
/// do atendimento cuida de quem vai junto.
/// </summary>
[ApiController]
[Route("pacientes/{pacienteId:guid}/acompanhantes")]
public sealed class AcompanhantesController(IAcompanhantesService service) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AcompanhanteDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AcompanhanteDto>> Listar(Guid pacienteId, CancellationToken cancellationToken) =>
        await service.ListarDoPacienteAsync(pacienteId, cancellationToken);

    /// <summary>Confere CPF + nascimento e devolve o nome para confirmar antes de cadastrar.</summary>
    [HttpPost("consulta")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType<AcompanhanteConsultaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<AcompanhanteConsultaDto> Consultar(
        Guid pacienteId,
        [FromBody] ConsultarAcompanhanteRequest request,
        CancellationToken cancellationToken) =>
        await service.ConsultarAsync(pacienteId, request, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType<AcompanhanteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<AcompanhanteDto> Adicionar(
        Guid pacienteId,
        [FromBody] AdicionarAcompanhanteRequest request,
        CancellationToken cancellationToken) =>
        await service.AdicionarAsync(pacienteId, request, OrigemCadastroAcompanhante.Painel, cancellationToken);

    [HttpDelete("{acompanhanteId:guid}")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(Guid pacienteId, Guid acompanhanteId, CancellationToken cancellationToken)
    {
        await service.RemoverAsync(pacienteId, acompanhanteId, OrigemCadastroAcompanhante.Painel, cancellationToken);
        return NoContent();
    }
}

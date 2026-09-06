using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Pacientes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Resolução do paciente dentro do fluxo de solicitação (plano 10): acha no cadastro local,
/// consulta o CADSUS quando não achar, e cria só em último caso.
///
/// <para>Rotas próprias, e não as de <c>PacientesController</c>, porque o que muda é a
/// <b>permissão</b>: quem abre solicitação tem o módulo <see cref="ModuloPermissao.Regulacao"/> e
/// não necessariamente o de Pacientes. A regra de cadastro é a mesma — o service reusa
/// <c>IPacientesService</c>.</para>
/// </summary>
[ApiController]
[Route("regulacao/pacientes")]
public sealed class RegulacaoPacientesController(IRegulacaoPacienteService servico) : ControllerBase
{
    [HttpGet("buscar")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<PacienteResumoRegulacaoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<PacienteResumoRegulacaoDto>> Buscar(
        [FromQuery] string termo, CancellationToken cancellationToken) =>
        servico.BuscarLocalAsync(termo, cancellationToken);

    /// <summary>
    /// Consulta o CADSUS pela porta configurada. Não cria nada — o operador confere e confirma.
    /// </summary>
    [HttpGet("cadsus")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<PacienteCadsusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<PacienteCadsusDto> Cadsus(
        [FromQuery] string documento, CancellationToken cancellationToken) =>
        servico.ConsultarCadsusAsync(documento, cancellationToken);

    /// <summary>Reusa o cadastro que existir (CNS → CPF) e só cria quando não há nenhum.</summary>
    [HttpPost("cadsus/confirmar")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Inclusao)]
    [ProducesResponseType<PacienteResumoRegulacaoDto>(StatusCodes.Status200OK)]
    public Task<PacienteResumoRegulacaoDto> ConfirmarCadsus(
        [FromBody] PacienteCadsusDto dto, CancellationToken cancellationToken) =>
        servico.ConfirmarCadsusAsync(dto, cancellationToken);

    /// <summary>409 <c>paciente.cpf_de_outro_cadastro</c> traz o id do outro para a tela oferecer a troca.</summary>
    [HttpPost("{id:guid}/cpf")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Edicao)]
    [ProducesResponseType<PacienteResumoRegulacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<PacienteResumoRegulacaoDto> InformarCpf(
        Guid id, [FromBody] InformarCpfRequest req, CancellationToken cancellationToken) =>
        servico.InformarCpfAsync(id, req.Cpf, cancellationToken);

    public sealed record InformarCpfRequest(string Cpf);
}

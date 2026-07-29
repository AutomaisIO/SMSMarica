using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Telefones;
using SMSMarica.Core.Telefones.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Validação do contato principal (WhatsApp) de uma pessoa por OTP, ancorado por CPF. Exige
/// apenas autenticação (qualquer usuário do painel) — a visibilidade do botão é controlada no
/// front pela permissão de edição do cadastro. O par (CPF, número) é único: 1 contato por CPF e
/// 1 dono por número.
/// </summary>
[ApiController]
[Route("telefones")]
public sealed class TelefonesController(
    ITelefoneValidacaoService service,
    IDispensaContatoService dispensas) : ControllerBase
{
    /// <summary>Dispara o envio do código de validação por WhatsApp para o contato do CPF.</summary>
    [HttpPost("validacao/enviar")]
    [ProducesResponseType<TelefoneOtpEmitidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<TelefoneOtpEmitidoDto> Enviar(
        [FromBody] EnviarTelefoneOtpRequest request, CancellationToken cancellationToken) =>
        await service.EnviarCodigoAsync(request.Cpf, request.Numero, cancellationToken);

    /// <summary>Confirma o código; em sucesso, registra o número como contato validado do CPF.</summary>
    [HttpPost("validacao/confirmar")]
    [ProducesResponseType<TelefoneValidadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<TelefoneValidadoDto> Confirmar(
        [FromBody] ConfirmarTelefoneOtpRequest request, CancellationToken cancellationToken) =>
        await service.ConfirmarCodigoAsync(request.Cpf, request.Numero, request.Codigo, cancellationToken);

    /// <summary>
    /// Define o telefone PRINCIPAL do paciente (por CPF) sem exigir verificação — edição
    /// rápida pelo painel. Trocar o número derruba o marcador de verificado; verificar
    /// depois é opcional.
    /// </summary>
    [HttpPost("principal")]
    [ProducesResponseType<TelefoneValidadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<TelefoneValidadoDto> DefinirPrincipal(
        [FromBody] DefinirTelefonePrincipalRequest request, CancellationToken cancellationToken) =>
        await service.DefinirPrincipalAsync(request.Cpf, request.Numero, cancellationToken);

    // Sem GET de "situação": o telefoneVerificado vem dentro do PacienteDto
    // (marcador no telecom do Patient FHIR) — nenhuma consulta própria necessária.

    // ---------------------------------------------------------------------------------------
    // Dispensa de verificação — o paciente que não tem celular (ou não consegue confirmar o
    // código) consente em não validar, com motivo. Sem isso a recepção ficava sem saída: a
    // autorização presencial do exame exige contato verificado.
    //
    // Permissão: mesma da recepção que autoriza o exame (SolicitacoesExame/Edição) — dispensar
    // é ato do balcão, não edição de cadastro.
    // ---------------------------------------------------------------------------------------

    /// <summary>Motivos disponíveis, com o efeito de cada um sobre o envio de resultado/laudo.</summary>
    [HttpGet("dispensa/motivos")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<MotivoDispensaContatoDto>>(StatusCodes.Status200OK)]
    public IReadOnlyList<MotivoDispensaContatoDto> MotivosDispensa() => dispensas.ListarMotivos();

    /// <summary>Dispensa ATIVA do paciente. 204 quando não há (o gate de verificado vale normal).</summary>
    [HttpGet("dispensa/{pacienteId:guid}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<DispensaContatoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<DispensaContatoDto>> DispensaAtiva(
        Guid pacienteId, CancellationToken cancellationToken)
    {
        var d = await dispensas.ObterAtivaAsync(pacienteId, cancellationToken);
        return d is null ? NoContent() : Ok(d);
    }

    /// <summary>Registra a dispensa (motivo + ciência do paciente). Substitui a ativa, se houver.</summary>
    [HttpPost("dispensa")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType<DispensaContatoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<DispensaContatoDto> RegistrarDispensa(
        [FromBody] RegistrarDispensaContatoRequest request, CancellationToken cancellationToken) =>
        await dispensas.RegistrarAsync(
            request.PacienteId, request.Motivo, request.MotivoDescricao, request.PacienteCiente,
            cancellationToken);

    /// <summary>Derruba a dispensa ativa (ex.: paciente voltou com celular). Idempotente.</summary>
    [HttpPost("dispensa/{pacienteId:guid}/revogar")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevogarDispensa(
        Guid pacienteId, [FromBody] RevogarDispensaContatoRequest? request, CancellationToken cancellationToken)
    {
        await dispensas.RevogarAsync(pacienteId, request?.Motivo, cancellationToken);
        return NoContent();
    }
}

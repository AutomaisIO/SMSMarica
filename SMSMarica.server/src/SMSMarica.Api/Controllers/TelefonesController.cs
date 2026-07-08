using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Telefones;
using SMSMarica.Core.Telefones.Dtos;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Validação do contato principal (WhatsApp) de uma pessoa por OTP, ancorado por CPF. Exige
/// apenas autenticação (qualquer usuário do painel) — a visibilidade do botão é controlada no
/// front pela permissão de edição do cadastro. O par (CPF, número) é único: 1 contato por CPF e
/// 1 dono por número.
/// </summary>
[ApiController]
[Route("telefones")]
public sealed class TelefonesController(ITelefoneValidacaoService service) : ControllerBase
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
}

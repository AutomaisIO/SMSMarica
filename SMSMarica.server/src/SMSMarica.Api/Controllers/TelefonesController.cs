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

    /// <summary>Situação do contato validado de um (CPF, número) — para exibir o selo.</summary>
    [HttpGet("validacao")]
    [ProducesResponseType<TelefoneValidadoDto>(StatusCodes.Status200OK)]
    public async Task<TelefoneValidadoDto> Consultar(
        [FromQuery] string cpf, [FromQuery] string numero, CancellationToken cancellationToken) =>
        await service.ConsultarAsync(cpf ?? string.Empty, numero ?? string.Empty, cancellationToken);

    /// <summary>
    /// Backfill contato_validado → marcador no telecom FHIR (transição para a fonte única;
    /// endpoint temporário — sai junto com a tabela na fase 2). Idempotente.
    /// </summary>
    [HttpPost("validacao/backfill-fhir")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Edicao)]
    [ProducesResponseType<TelefoneBackfillResultadoDto>(StatusCodes.Status200OK)]
    public async Task<TelefoneBackfillResultadoDto> BackfillFhir(CancellationToken cancellationToken) =>
        await service.BackfillFhirAsync(cancellationToken);
}

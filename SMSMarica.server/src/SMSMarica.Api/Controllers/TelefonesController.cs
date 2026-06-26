using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Telefones;
using SMSMarica.Core.Telefones.Dtos;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Validação de número de telefone por OTP (WhatsApp). Exige apenas autenticação (qualquer
/// usuário do painel) — a visibilidade do botão é controlada no front pela permissão de
/// edição do cadastro em questão. A validação é do número em si (registro global).
/// </summary>
[ApiController]
[Route("telefones")]
public sealed class TelefonesController(ITelefoneValidacaoService service) : ControllerBase
{
    /// <summary>Dispara o envio do código de validação por WhatsApp.</summary>
    [HttpPost("validacao/enviar")]
    [ProducesResponseType<TelefoneOtpEmitidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<TelefoneOtpEmitidoDto> Enviar(
        [FromBody] EnviarTelefoneOtpRequest request, CancellationToken cancellationToken) =>
        await service.EnviarCodigoAsync(request.Numero, cancellationToken);

    /// <summary>Confirma o código; em caso de sucesso, registra o número como validado.</summary>
    [HttpPost("validacao/confirmar")]
    [ProducesResponseType<TelefoneValidadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<TelefoneValidadoDto> Confirmar(
        [FromBody] ConfirmarTelefoneOtpRequest request, CancellationToken cancellationToken) =>
        await service.ConfirmarCodigoAsync(request.Numero, request.Codigo, cancellationToken);

    /// <summary>Situação de validação de um número (para exibir o selo).</summary>
    [HttpGet("validacao")]
    [ProducesResponseType<TelefoneValidadoDto>(StatusCodes.Status200OK)]
    public async Task<TelefoneValidadoDto> Consultar(
        [FromQuery] string numero, CancellationToken cancellationToken)
    {
        var r = await service.ConsultarAsync([numero ?? string.Empty], cancellationToken);
        return r.Count > 0 ? r[0] : new TelefoneValidadoDto(numero ?? string.Empty, false, null);
    }
}

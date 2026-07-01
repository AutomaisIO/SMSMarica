using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Cidadao;
using SMSMarica.Core.Cidadao.Dtos;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Login do paciente no PWA: CPF + código (OTP). Anônimo (o paciente ainda não tem token).
/// Em modo de teste o código volta no corpo para ser exibido na tela.
/// </summary>
[ApiController]
[Route("auth/paciente")]
[AllowAnonymous]
public sealed class AuthPacienteController(
    IPacienteAuthService service,
    ICidadaoLoginLinkService loginLinks) : ControllerBase
{
    private readonly IPacienteAuthService _service = service;
    private readonly ICidadaoLoginLinkService _loginLinks = loginLinks;

    [HttpPost("solicitar-otp")]
    [ProducesResponseType<OtpEmitidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<OtpEmitidoDto> SolicitarOtp([FromBody] SolicitarOtpRequest request, CancellationToken cancellationToken) =>
        await _service.SolicitarOtpAsync(request, cancellationToken);

    [HttpPost("validar-otp")]
    [ProducesResponseType<RespostaLoginPacienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<RespostaLoginPacienteDto> ValidarOtp([FromBody] ValidarOtpRequest request, CancellationToken cancellationToken) =>
        await _service.ValidarOtpAsync(
            request,
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

    /// <summary>
    /// Troca o "magic-link" (código do link do WhatsApp) por uma sessão — login em 1
    /// clique. Uso único: 410 se já usado/expirado/inexistente (o app manda pro login).
    /// </summary>
    [HttpPost("magic")]
    [ProducesResponseType<RespostaMagicLinkDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Magic([FromBody] MagicLinkTrocaRequest request, CancellationToken cancellationToken)
    {
        var ua = Request.Headers.UserAgent.ToString() is { Length: > 0 } u ? u : null;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var r = await _loginLinks.TrocarAsync(request.Token, ua, ip, cancellationToken);
        return r is null ? StatusCode(StatusCodes.Status410Gone) : Ok(r);
    }
}

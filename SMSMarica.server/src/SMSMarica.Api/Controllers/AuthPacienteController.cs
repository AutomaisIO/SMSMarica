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
public sealed class AuthPacienteController(IPacienteAuthService service) : ControllerBase
{
    private readonly IPacienteAuthService _service = service;

    [HttpPost("solicitar-otp")]
    [ProducesResponseType<OtpEmitidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<OtpEmitidoDto> SolicitarOtp([FromBody] SolicitarOtpRequest request, CancellationToken cancellationToken) =>
        await _service.SolicitarOtpAsync(request, cancellationToken);

    [HttpPost("validar-otp")]
    [ProducesResponseType<RespostaLoginPacienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<RespostaLoginPacienteDto> ValidarOtp([FromBody] ValidarOtpRequest request, CancellationToken cancellationToken) =>
        await _service.ValidarOtpAsync(request, cancellationToken);
}

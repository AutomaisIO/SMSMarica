using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Cidadao.Dtos;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Login do paciente no PWA: CPF + código (OTP). Anônimo (o paciente ainda não tem token).
/// Em modo de teste o código volta no corpo para ser exibido na tela.
/// </summary>
[ApiController]
[Route("auth/paciente")]
[AllowAnonymous]
[EnableRateLimiting("login-cidadao")]
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

    /// <summary>
    /// Passo 2 de quem não tem contato verificado (ou perdeu o número): nascimento + nº da
    /// solicitação SISREG + telefone que vai receber o código. Sem cadastro, o par CPF/nascimento
    /// é conferido na Receita e o paciente é criado ao confirmar o código.
    /// </summary>
    [HttpPost("solicitar-otp-verificacao")]
    [ProducesResponseType<OtpEmitidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<OtpEmitidoDto> SolicitarOtpVerificacao(
        [FromBody] SolicitarOtpVerificacaoRequest request, CancellationToken cancellationToken) =>
        await _service.SolicitarOtpVerificacaoAsync(request, cancellationToken);

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
    /// <para>Links que carregam RESULTADO clínico não autenticam aqui: devolvem
    /// <c>requerConfirmacaoCpf</c> sem consumir o token, e o app chama
    /// <see cref="MagicConfirmar"/>.</para>
    /// </summary>
    [HttpPost("magic")]
    [ProducesResponseType<RespostaMagicLinkDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Magic([FromBody] MagicLinkTrocaRequest request, CancellationToken cancellationToken)
    {
        var ua = Request.Headers.UserAgent.ToString() is { Length: > 0 } u ? u : null;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var r = await _loginLinks.TrocarAsync(request.Token, ua, ip, cancellationToken: cancellationToken);
        return r is null ? StatusCode(StatusCodes.Status410Gone) : Ok(r);
    }

    /// <summary>
    /// 2º passo dos links clínicos: confirma o CPF do titular e só então abre a sessão.
    /// CPF errado devolve o desafio com uma tentativa a menos; na 3ª o link é queimado (410)
    /// e a recepção precisa reenviar.
    /// </summary>
    [HttpPost("magic/confirmar")]
    [ProducesResponseType<RespostaMagicLinkDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> MagicConfirmar(
        [FromBody] MagicLinkConfirmarRequest request, CancellationToken cancellationToken)
    {
        var ua = Request.Headers.UserAgent.ToString() is { Length: > 0 } u ? u : null;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var r = await _loginLinks.TrocarAsync(request.Token, ua, ip, request.Cpf, cancellationToken);
        return r is null ? StatusCode(StatusCodes.Status410Gone) : Ok(r);
    }
}

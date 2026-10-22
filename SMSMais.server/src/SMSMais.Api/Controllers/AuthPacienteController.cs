using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Cidadao.Dtos;
using SMSMais.Core.Cidadao.Entrada;
using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Entrada do paciente no app (PWA e nativo, ADR-0078): CPF + data de nascimento → número escolhido
/// → código (OTP). Anônimo (o paciente ainda não tem token). Em modo de teste o código volta no corpo
/// para ser exibido na tela.
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

    /// <summary>Passo 1: CPF + data de nascimento → os números possíveis (só o final de cada um).</summary>
    [HttpPost("entrada")]
    [ProducesResponseType<EntradaCidadaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<EntradaCidadaoDto> IniciarEntrada(
        [FromBody] IniciarEntradaRequest request, CancellationToken cancellationToken) =>
        await _service.IniciarEntradaAsync(request, Request.Headers[CanalAppCidadao.Cabecalho].ToString(), cancellationToken);

    /// <summary>"Nenhum desses é meu": procura outros celulares no e-SUS, quando o canal permite.</summary>
    [HttpPost("entrada/{desafioId}/esus")]
    [ProducesResponseType<EntradaCidadaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<EntradaCidadaoDto> BuscarNoEsus(string desafioId, CancellationToken cancellationToken) =>
        await _service.BuscarNoEsusAsync(desafioId, cancellationToken);

    /// <summary>Envia o código para o número escolhido.</summary>
    [HttpPost("entrada/{desafioId}/enviar")]
    [ProducesResponseType<OtpEmitidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<OtpEmitidoDto> EnviarCodigo(
        string desafioId, [FromBody] EnviarCodigoEntradaRequest request, CancellationToken cancellationToken) =>
        await _service.EnviarCodigoEntradaAsync(desafioId, request, cancellationToken);

    /// <summary>
    /// A entrada antiga (CPF, depois nº da solicitação + número digitado) saiu em 22/10/2026 (ADR-0078).
    /// 410 para o PWA em cache pedir para recarregar.
    /// </summary>
    [HttpPost("solicitar-otp")]
    [HttpPost("solicitar-otp-verificacao")]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public IActionResult EntradaAntiga() =>
        throw new RecursoDescontinuadoException(
            "entrada.atualizar", "Atualize o aplicativo (ou recarregue a página) para entrar.");

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

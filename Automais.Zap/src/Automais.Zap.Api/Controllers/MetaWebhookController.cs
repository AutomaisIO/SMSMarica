using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Relay;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Automais.Zap.Api.Controllers;

/// <summary>
/// O único endpoint que a Meta conhece. É esta URL que vai na Callback URL do App.
/// Anônimo por natureza — a autorização é a assinatura HMAC do corpo.
/// </summary>
[ApiController]
[Route("meta")]
[EnableRateLimiting("meta-webhook")]
public sealed class MetaWebhookController(
    IRelayService relay,
    IConfiguracaoMetaService configuracao,
    ILogger<MetaWebhookController> logger) : ControllerBase
{
    /// <summary>Handshake de verificação do webhook (a Meta chama uma vez, ao configurar).</summary>
    [HttpGet("webhook")]
    public async Task<IActionResult> Verificar(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        CancellationToken cancellationToken)
    {
        var esperado = (await configuracao.ObterAsync(cancellationToken)).VerifyToken;
        if (string.IsNullOrWhiteSpace(esperado))
        {
            logger.LogError("Meta:VerifyToken não configurado — handshake recusado.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var a = System.Text.Encoding.UTF8.GetBytes(verifyToken ?? "");
        var b = System.Text.Encoding.UTF8.GetBytes(esperado);
        var confere = a.Length == b.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
        if (mode == "subscribe" && confere)
        {
            return Content(challenge ?? string.Empty, "text/plain");
        }

        logger.LogWarning("Handshake recusado (mode={Mode}).", mode);
        return Unauthorized();
    }

    /// <summary>
    /// Recebe o evento, entrega ao dono do número e responde de acordo.
    ///
    /// O 200 só sai depois da entrega. Falhou algum destino, responde 5xx e a Meta reentrega o
    /// lote — é o que substitui uma tabela de buffer aqui dentro. Um destino que já tinha
    /// recebido vê a duplicata, absorvida pela idempotência por wamid que a instância já tem.
    /// </summary>
    [HttpPost("webhook")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Receber(CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var corpo = buffer.ToArray();

        var assinatura = Request.Headers["X-Hub-Signature-256"].ToString();
        var resultado = await relay.ProcessarAsync(corpo, assinatura, cancellationToken);

        return resultado.Situacao switch
        {
            SituacaoRelay.NaoConfigurado => StatusCode(StatusCodes.Status503ServiceUnavailable),
            SituacaoRelay.AssinaturaInvalida => Unauthorized(),
            SituacaoRelay.PayloadInvalido => BadRequest(),

            // 502: a falha é do destino, não do payload. A Meta trata qualquer não-2xx como
            // "reentregar", que é exatamente o que se quer.
            SituacaoRelay.FalhaDeEntrega => StatusCode(StatusCodes.Status502BadGateway),

            // Inclui o caso "nenhuma rota encontrada": 200 de propósito. Reentregar um evento
            // de número não cadastrado não faria ele passar a existir — só encheria o log.
            _ => Ok(),
        };
    }
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Tfd.Configuracao;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Webhook do WhatsApp Cloud API (Meta). Anônimo — a autorização é a validação da
/// assinatura <c>X-Hub-Signature-256</c> (HMAC com o App Secret) quando configurada.
/// </summary>
[ApiController]
[Route("integracoes/whatsapp")]
[AllowAnonymous]
public sealed class WhatsAppWebhookController(
    IWhatsAppWebhookService webhook,
    ITfdConfigService config,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    /// <summary>Verificação de assinatura do webhook (handshake da Meta).</summary>
    [HttpGet("webhook")]
    public async Task<IActionResult> Verificar(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        CancellationToken cancellationToken)
    {
        TfdWhatsAppContexto ctx;
        try { ctx = await config.ObterWhatsAppContextoAsync(cancellationToken); }
        catch { return Forbid(); }

        if (mode == "subscribe" && !string.IsNullOrEmpty(ctx.VerifyToken) && verifyToken == ctx.VerifyToken)
        {
            return Content(challenge ?? string.Empty, "text/plain");
        }
        return Unauthorized();
    }

    /// <summary>Recebe eventos (mensagens/status). Valida assinatura se App Secret configurado.</summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Receber(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        string raw;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            raw = await reader.ReadToEndAsync(cancellationToken);
        }
        Request.Body.Position = 0;

        try
        {
            var ctx = await config.ObterWhatsAppContextoAsync(cancellationToken);
            if (!string.IsNullOrEmpty(ctx.AppSecret))
            {
                var assinatura = Request.Headers["X-Hub-Signature-256"].ToString();
                var esperado = "sha256=" + Convert.ToHexString(
                    HMACSHA256.HashData(Encoding.UTF8.GetBytes(ctx.AppSecret), Encoding.UTF8.GetBytes(raw)))
                    .ToLowerInvariant();
                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(assinatura), Encoding.UTF8.GetBytes(esperado)))
                {
                    logger.LogWarning("Webhook WhatsApp: assinatura inválida.");
                    return Unauthorized();
                }
            }
        }
        catch
        {
            // Sem configuração (modo teste) — segue sem validar assinatura.
        }

        await webhook.ProcessarAsync(raw, cancellationToken);
        return Ok();
    }
}

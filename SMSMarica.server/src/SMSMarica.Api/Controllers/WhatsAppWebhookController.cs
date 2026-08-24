using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Tfd.Configuracao;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Webhook por onde o <b>Automais.Zap</b> entrega os eventos do WhatsApp desta instância.
///
/// <para>Só ele chama aqui — a Meta não fala mais direto com este sistema (ADR-0044). A
/// autorização é a assinatura <c>X-Automais-Signature</c>: HMAC-SHA256 do segredo combinado
/// entre os dois sistemas sobre <c>{timestamp}.{corpo}</c>, com o instante em
/// <c>X-Automais-Timestamp</c>. O timestamp entra no que é assinado para que capturar uma
/// entrega não permita reenviá-la depois.</para>
///
/// <para>O payload em si continua no formato da Cloud API — o relay o repassa intacto —, por
/// isso o processamento a jusante não mudou.</para>
/// </summary>
[ApiController]
[Route("integracoes/whatsapp")]
[AllowAnonymous]
public sealed class WhatsAppWebhookController(
    IWhatsAppWebhookService webhook,
    ITfdConfigService config,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    private const string CabecalhoAssinatura = "X-Automais-Signature";
    private const string CabecalhoTimestamp = "X-Automais-Timestamp";

    /// <summary>Tolerância de relógio entre o relay e esta instância.</summary>
    private static readonly TimeSpan JanelaAssinatura = TimeSpan.FromMinutes(5);

    /// <summary>Recebe eventos (mensagens e status) entregues pelo Automais.Zap.</summary>
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

        TfdWhatsAppContexto ctx;
        try
        {
            ctx = await config.ObterWhatsAppContextoAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // FALHA FECHADO. Sem configuração não há como conferir a origem — e aceitar sem
            // conferir é deixar qualquer um criar conversa.
            logger.LogError(ex, "Webhook WhatsApp: sem configuração para validar a origem. Recusando.");
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(ctx.ZapSegredoWebhook))
        {
            logger.LogError("Webhook WhatsApp: segredo do Automais.Zap não configurado em Integrações. Recusando.");
            return Unauthorized();
        }

        if (!AssinaturaConfere(raw, ctx.ZapSegredoWebhook))
        {
            return Unauthorized();
        }

        await webhook.ProcessarAsync(raw, cancellationToken);
        return Ok();
    }

    private bool AssinaturaConfere(string raw, string segredo)
    {
        var assinatura = Request.Headers[CabecalhoAssinatura].ToString();
        if (string.IsNullOrWhiteSpace(assinatura))
        {
            logger.LogWarning("Webhook WhatsApp: chamada sem {Cabecalho}.", CabecalhoAssinatura);
            return false;
        }

        if (!long.TryParse(Request.Headers[CabecalhoTimestamp].ToString(), out var ts))
        {
            logger.LogWarning("Webhook WhatsApp: assinatura sem timestamp.");
            return false;
        }

        var idade = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(ts);
        if (idade.Duration() > JanelaAssinatura)
        {
            logger.LogWarning("Webhook WhatsApp: assinatura fora da janela ({Idade}).", idade);
            return false;
        }

        var esperado = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(segredo),
            Encoding.UTF8.GetBytes($"{ts}.{raw}"))).ToLowerInvariant();

        var a = Encoding.UTF8.GetBytes(assinatura.Trim());
        var b = Encoding.UTF8.GetBytes(esperado);
        if (a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b)) return true;

        logger.LogWarning("Webhook WhatsApp: assinatura do Automais.Zap inválida.");
        return false;
    }
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Tfd.Configuracao;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Webhook do WhatsApp. Anônimo — a autorização é a assinatura do corpo.
///
/// <para>Duas origens são aceitas, de propósito, porque durante a migração as duas existem:</para>
/// <list type="bullet">
/// <item><b>Automais.Zap</b> — <c>X-Automais-Signature</c> sobre <c>{timestamp}.{corpo}</c>,
/// com o segredo combinado entre os dois sistemas. É o caminho novo.</item>
/// <item><b>Meta direto</b> — <c>X-Hub-Signature-256</c> com o App Secret. É o caminho antigo,
/// que continua valendo enquanto o App antigo estiver inscrito no WABA.</item>
/// </list>
/// </summary>
[ApiController]
[Route("integracoes/whatsapp")]
[AllowAnonymous]
public sealed class WhatsAppWebhookController(
    IWhatsAppWebhookService webhook,
    ITfdConfigService config,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    /// <summary>Tolerância de relógio na assinatura do Automais.Zap.</summary>
    private static readonly TimeSpan JanelaAssinatura = TimeSpan.FromMinutes(5);

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

    /// <summary>Recebe eventos (mensagens/status).</summary>
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
            // FALHA FECHADO. Antes havia um catch vazio aqui e o POST seguia SEM conferir
            // assinatura nenhuma quando a configuração não carregava — qualquer um criava
            // conversa. Com App compartilhado entre municípios isso é pior ainda.
            logger.LogError(ex, "Webhook WhatsApp: sem configuração para validar a origem. Recusando.");
            return Unauthorized();
        }

        if (!OrigemConfiavel(raw, ctx))
        {
            return Unauthorized();
        }

        await webhook.ProcessarAsync(raw, cancellationToken);
        return Ok();
    }

    private bool OrigemConfiavel(string raw, TfdWhatsAppContexto ctx)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);

        // 1) Automais.Zap, quando o segredo estiver combinado.
        var assinaturaZap = Request.Headers["X-Automais-Signature"].ToString();
        if (!string.IsNullOrWhiteSpace(ctx.ZapSegredoWebhook) && !string.IsNullOrWhiteSpace(assinaturaZap))
        {
            if (!long.TryParse(Request.Headers["X-Automais-Timestamp"].ToString(), out var ts))
            {
                logger.LogWarning("Webhook: assinatura do Automais.Zap sem timestamp.");
                return false;
            }

            var idade = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(ts);
            if (idade.Duration() > JanelaAssinatura)
            {
                // Sem esta janela, capturar uma entrega permitiria reenviá-la depois.
                logger.LogWarning("Webhook: assinatura do Automais.Zap fora da janela ({Idade}).", idade);
                return false;
            }

            var esperado = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(ctx.ZapSegredoWebhook),
                Encoding.UTF8.GetBytes($"{ts}.{raw}"))).ToLowerInvariant();

            if (Iguais(assinaturaZap, esperado)) return true;

            logger.LogWarning("Webhook: assinatura do Automais.Zap inválida.");
            return false;
        }

        // 2) Meta direto, enquanto o App antigo continuar inscrito no WABA.
        var assinaturaMeta = Request.Headers["X-Hub-Signature-256"].ToString();
        if (!string.IsNullOrEmpty(ctx.AppSecret) && !string.IsNullOrWhiteSpace(assinaturaMeta))
        {
            var esperado = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(ctx.AppSecret), Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

            if (Iguais(assinaturaMeta, esperado)) return true;

            logger.LogWarning("Webhook WhatsApp: assinatura da Meta inválida.");
            return false;
        }

        logger.LogWarning("Webhook: nenhuma assinatura reconhecível e nenhum segredo configurado.");
        return false;
    }

    private static bool Iguais(string recebida, string esperada)
    {
        var a = Encoding.UTF8.GetBytes(recebida.Trim());
        var b = Encoding.UTF8.GetBytes(esperada);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

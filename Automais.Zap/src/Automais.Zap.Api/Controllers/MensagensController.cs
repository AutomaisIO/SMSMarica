using System.Text.Json;
using System.Text.Json.Serialization;
using Automais.Zap.Core.Envio;
using Automais.Zap.Core.Tokens;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Automais.Zap.Api.Controllers;

public sealed class EnviarMensagemRequest
{
    /// <summary>Linha pela qual enviar. É o <c>phone_number_id</c> da Meta.</summary>
    [JsonPropertyName("phone_number_id")]
    public string? PhoneNumberId { get; set; }

    /// <summary>Destinatário, em E.164 ou só dígitos.</summary>
    public string? Para { get; set; }

    /// <summary>
    /// Corpo no formato da Cloud API: <c>{"type":"text","text":{"body":"..."}}</c> ou
    /// <c>{"type":"template","template":{...}}</c>. Vai adiante como está.
    /// </summary>
    public JsonElement Mensagem { get; set; }
}

/// <summary>
/// API de envio para o sistema do cliente. Autenticada por token de tenant
/// (<c>Authorization: Bearer zap_…</c>), gerado no painel.
///
/// <para><b>Envia agora e não guarda nada.</b> Agendamento e janela de silêncio são do sistema
/// do cliente, que tem a máquina de retentativa e o contexto para decidir a hora. Uma fila aqui
/// obrigaria o relay a reter o texto até o disparo, e é justamente isso que ele promete não
/// fazer.</para>
/// </summary>
[ApiController]
[Route("v1/mensagens")]
[EnableRateLimiting("api-publica")]
public sealed class MensagensController(
    ITokenService tokens,
    IEnvioService envio,
    ILogger<MensagensController> logger) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(256_000)]
    public async Task<IActionResult> Enviar([FromBody] EnviarMensagemRequest corpo, CancellationToken ct)
    {
        var chamador = await tokens.AutenticarAsync(Request.Headers.Authorization.ToString(), ct);
        if (chamador is null)
        {
            logger.LogWarning("Chamada a /v1/mensagens com token inválido, revogado ou de tenant suspenso.");
            return Unauthorized(new { erro = "Token inválido, revogado ou tenant suspenso." });
        }

        if (string.IsNullOrWhiteSpace(corpo.PhoneNumberId) || string.IsNullOrWhiteSpace(corpo.Para))
        {
            return BadRequest(new { erro = "phone_number_id e para são obrigatórios." });
        }

        if (corpo.Mensagem.ValueKind != JsonValueKind.Object)
        {
            return BadRequest(new { erro = "mensagem precisa ser um objeto no formato da Cloud API." });
        }

        var r = await envio.EnviarAsync(
            chamador, new PedidoEnvio(corpo.PhoneNumberId, corpo.Para, corpo.Mensagem), ct);

        if (r.Sucesso) return Ok(new { wamid = r.Wamid });

        // 403 é decisão nossa (token sem acesso ao número); 503 é falta de configuração da
        // plataforma; o resto é a Meta recusando, e o texto dela vale mais que um genérico.
        return StatusCode(r.StatusHttp is 403 or 503 ? r.StatusHttp.Value : StatusCodes.Status502BadGateway,
            new { erro = r.Erro });
    }
}

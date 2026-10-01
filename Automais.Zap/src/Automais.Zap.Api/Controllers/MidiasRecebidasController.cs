using Automais.Zap.Core.Midias;
using Automais.Zap.Core.Tokens;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Automais.Zap.Api.Controllers;

/// <summary>
/// Download da mídia que um cidadão mandou (foto, PDF, áudio, vídeo). O webhook só traz o id da
/// mídia; o arquivo mora na Meta e só sai com o token do System User, que é do relay.
///
/// <para>Autenticado por token de tenant, como o envio. Não guarda nada: os bytes vão direto para
/// quem pediu.</para>
/// </summary>
[ApiController]
[Route("v1/midias-recebidas")]
[EnableRateLimiting("api-publica")]
public sealed class MidiasRecebidasController(
    ITokenService tokens,
    IMidiaRecebidaService midias) : ControllerBase
{
    /// <summary>Teto padrão: 25 MB. Documento na Cloud API vai até 100 MB, mas ninguém precisa disso aqui.</summary>
    private const long TetoPadrao = 25 * 1024 * 1024;

    [HttpGet("{mediaId}")]
    public async Task<IActionResult> Baixar(
        string mediaId,
        [FromQuery(Name = "phone_number_id")] string? phoneNumberId,
        [FromQuery(Name = "max_bytes")] long? maxBytes,
        CancellationToken ct)
    {
        var chamador = await tokens.AutenticarAsync(Request.Headers.Authorization.ToString(), ct);
        if (chamador is null) return Unauthorized(new { erro = "Token inválido, revogado ou tenant suspenso." });

        if (string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return BadRequest(new { erro = "phone_number_id é obrigatório." });
        }

        var teto = maxBytes is > 0 and <= TetoPadrao ? maxBytes.Value : TetoPadrao;
        var r = await midias.BaixarAsync(chamador, phoneNumberId, mediaId, teto, ct);
        if (r.Midia is { } m)
        {
            if (m.Sha256 is { Length: > 0 }) Response.Headers["X-Meta-Sha256"] = m.Sha256;
            return File(m.Conteudo, m.MimeType);
        }

        return StatusCode(r.Status, new { erro = r.Erro });
    }
}

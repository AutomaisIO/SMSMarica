using Automais.Zap.Core.Midias;
using Automais.Zap.Core.Tokens;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Automais.Zap.Api.Controllers;

/// <summary>
/// Arquivos que a plataforma hospeda para a Meta baixar — hoje, a arte do cabeçalho dos modelos
/// com foto no topo.
///
/// <para>Existe aqui, e não em cada instância, porque a arte é ativo do canal: a Meta rebaixa a
/// URL a <b>cada</b> mensagem enviada. Centralizar dá um endereço só, estável, que não depende
/// de o município ter onde publicar arquivo estático — e tira do caminho o passo de "commitar a
/// imagem no front e esperar o deploy".</para>
///
/// <para><b>Nada de CORS aqui:</b> quem busca o arquivo é o servidor da Meta, não um navegador.
/// O GET é anônimo porque tem de ser — a Meta baixa sem credencial nenhuma.</para>
/// </summary>
[ApiController]
[Route("v1/midias")]
[EnableRateLimiting("api-publica")]
public sealed class MidiasController(
    ITokenService tokens,
    IMidiaService midias,
    ILogger<MidiasController> logger) : ControllerBase
{
    /// <summary>Sobe um arquivo para o tenant do token. PNG ou JPEG, até 5 MB (limites da Meta).</summary>
    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Enviar(
        IFormFile? arquivo, [FromForm] string? categoria, CancellationToken ct)
    {
        var chamador = await tokens.AutenticarAsync(Request.Headers.Authorization.ToString(), ct);
        if (chamador is null) return Unauthorized(new { erro = "Token inválido, revogado ou tenant suspenso." });

        if (arquivo is null || arquivo.Length == 0) return BadRequest(new { erro = "Arquivo não enviado." });

        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, ct);

        var (midia, erro) = await midias.GuardarAsync(
            chamador.TenantId, arquivo.FileName, arquivo.ContentType, ms.ToArray(),
            categoria, usuarioId: null, ct);
        if (midia is null) return BadRequest(new { erro });

        logger.LogInformation(
            "Mídia {Id} ({Bytes} bytes) guardada para o tenant {Tenant}.",
            midia.Id, midia.TamanhoBytes, chamador.TenantId);

        return Ok(Descrever(midia));
    }

    /// <summary>O que o tenant já subiu (sem os binários).</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? categoria, CancellationToken ct)
    {
        var chamador = await tokens.AutenticarAsync(Request.Headers.Authorization.ToString(), ct);
        if (chamador is null) return Unauthorized(new { erro = "Token inválido, revogado ou tenant suspenso." });

        var lista = await midias.ListarAsync(chamador.TenantId, categoria, ct);
        return Ok(new { midias = lista.Select(Descrever) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Apagar(Guid id, CancellationToken ct)
    {
        var chamador = await tokens.AutenticarAsync(Request.Headers.Authorization.ToString(), ct);
        if (chamador is null) return Unauthorized(new { erro = "Token inválido, revogado ou tenant suspenso." });

        // Apagar arte ainda referenciada por um modelo quebra o envio daquele modelo — quem
        // decide isso é o painel do cliente, que sabe o que está em uso.
        return await midias.ApagarAsync(chamador.TenantId, id, ct) ? NoContent() : NotFound();
    }

    private object Descrever(MidiaGuardada m) => new
    {
        id = m.Id,
        // Absoluta de propósito: é este endereço que vai no componente de cabeçalho do envio.
        url = $"{Request.Scheme}://{Request.Host}{m.Caminho}",
        nome = m.NomeArquivo,
        mime = m.MimeType,
        bytes = m.TamanhoBytes,
        largura = m.Largura,
        altura = m.Altura,
        criado_em = m.CriadoEm,
    };
}

/// <summary>
/// Serve o binário. Fora de <c>/v1</c> e sem token: é a URL que a Meta baixa, e uma URL que exige
/// credencial não serve para nada num cabeçalho de modelo.
/// </summary>
[ApiController]
[Route("midias")]
public sealed class MidiasPublicasController(IMidiaService midias) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var conteudo = await midias.ObterConteudoAsync(id, ct);
        if (conteudo is null) return NotFound();

        // O id é por conteúdo (hash) e o arquivo nunca muda: a Meta pode cachear à vontade.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(conteudo.Conteudo, conteudo.MimeType);
    }
}

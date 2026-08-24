using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Midias;
using SMSMarica.Core.Midias.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Armazenamento genérico de imagens/binários no banco (bytea). Reutilizável por
/// qualquer feature. O upload é autenticado; servir o binário é anônimo — são
/// ativos institucionais (logo, timbre) referenciados por <c>&lt;img src&gt;</c>,
/// que o browser carrega sem header de autenticação.
/// </summary>
[ApiController]
[Route("midias")]
public sealed class MidiasController(IMidiasService service) : ControllerBase
{
    private readonly IMidiasService _service = service;

    /// <summary>Envia uma imagem e devolve seus metadados (incl. a URL para usar como <c>src</c>).</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.ConfiguracaoLaudo, AcoesPermissao.Edicao)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType<MidiaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enviar(
        IFormFile arquivo,
        [FromForm] string? categoria,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { erro = "Arquivo não enviado." });
        }

        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, cancellationToken);

        var dto = await _service.EnviarAsync(
            ExtrairUsuarioId(),
            arquivo.FileName,
            arquivo.ContentType,
            ms.ToArray(),
            categoria,
            cancellationToken);

        return CreatedAtAction(nameof(Obter), new { id = dto.Id }, dto);
    }

    /// <summary>Serve o binário da mídia. Anônimo (referenciado por <c>&lt;img src&gt;</c>).</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [Produces("image/png", "image/jpeg", "image/gif", "image/webp", "image/svg+xml")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        var conteudo = await _service.ObterConteudoAsync(id, cancellationToken);
        if (conteudo is null) return NotFound();

        // Conteúdo imutável (id por hash não muda) → cache agressivo.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(conteudo.Conteudo, conteudo.MimeType, conteudo.NomeArquivo);
    }

    private Guid? ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

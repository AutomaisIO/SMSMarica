using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Midias;
using SMSMarica.Core.Midias.Dtos;
using SMSMarica.Core.Tickets;
using SMSMarica.Core.Tickets.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Suporte / gestão de tickets. Os endpoints em <c>/tickets</c> são self-service (qualquer
/// usuário autenticado abre e acompanha os próprios tickets). Os endpoints em <c>/tickets/gestao</c>
/// exigem o módulo <see cref="ModuloPermissao.Ticket"/> (equipe/admin): veem todos, respondem,
/// mudam status e configuram a visibilidade.
/// </summary>
[ApiController]
[Route("tickets")]
public sealed class TicketsController(ITicketService service, IMidiasService midias) : ControllerBase
{
    private readonly ITicketService _service = service;
    private readonly IMidiasService _midias = midias;

    // ---------------- Self-service (autenticado) ----------------

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TicketListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<TicketListItemDto>> Listar([FromQuery] bool incluirArquivados, CancellationToken ct)
        => await _service.ListarVisiveisAsync(incluirArquivados, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<TicketDto> Obter(Guid id, CancellationToken ct) => await _service.ObterAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Abrir([FromBody] AbrirTicketRequest req, CancellationToken ct)
    {
        var id = await _service.AbrirAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id }, id);
    }

    [HttpPost("{id:guid}/comentarios")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Comentar(Guid id, [FromBody] ComentarTicketRequest req, CancellationToken ct)
    {
        await _service.ComentarAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/arquivar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Arquivar(Guid id, [FromQuery] bool arquivar, CancellationToken ct)
    {
        await _service.ArquivarComoAutorAsync(id, arquivar, ct);
        return NoContent();
    }

    [HttpGet("configuracao")]
    [ProducesResponseType<TicketConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<TicketConfiguracaoDto> ObterConfiguracao(CancellationToken ct) => await _service.ObterConfiguracaoAsync(ct);

    /// <summary>Envia um print/imagem e devolve a mídia (para referenciar ao abrir/comentar). Autenticado.</summary>
    [HttpPost("anexos")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType<MidiaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnviarAnexo(IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { erro = "Arquivo não enviado." });
        }

        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, ct);
        var dto = await _midias.EnviarAsync(ExtrairUsuarioId(), arquivo.FileName, arquivo.ContentType, ms.ToArray(), "ticket", ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    // ---------------- Gestão (módulo Ticket) ----------------

    [HttpGet("gestao")]
    [RequerPermissao(ModuloPermissao.Ticket, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<TicketListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<TicketListItemDto>> ListarTodos([FromQuery] bool incluirArquivados, CancellationToken ct)
        => await _service.ListarTodosAsync(incluirArquivados, ct);

    [HttpGet("gestao/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Ticket, AcoesPermissao.Consulta)]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    public async Task<TicketDto> ObterGestao(Guid id, CancellationToken ct) => await _service.ObterGestaoAsync(id, ct);

    [HttpPost("gestao/{id:guid}/comentarios")]
    [RequerPermissao(ModuloPermissao.Ticket, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ComentarGestao(Guid id, [FromBody] ComentarTicketRequest req, CancellationToken ct)
    {
        await _service.ComentarGestaoAsync(id, req, ct);
        return NoContent();
    }

    [HttpPut("gestao/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Ticket, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarGestao(Guid id, [FromBody] AtualizarTicketGestaoRequest req, CancellationToken ct)
    {
        await _service.AtualizarGestaoAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("gestao/{id:guid}/arquivar")]
    [RequerPermissao(ModuloPermissao.Ticket, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ArquivarGestao(Guid id, [FromQuery] bool arquivar, CancellationToken ct)
    {
        await _service.ArquivarComoAdminAsync(id, arquivar, ct);
        return NoContent();
    }

    [HttpDelete("gestao/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Ticket, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _service.ExcluirAsync(id, ct);
        return NoContent();
    }

    [HttpPut("gestao/configuracao")]
    [RequerPermissao(ModuloPermissao.Ticket, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarVisibilidade([FromBody] AtualizarVisibilidadeRequest req, CancellationToken ct)
    {
        await _service.AtualizarVisibilidadeAsync(req, ct);
        return NoContent();
    }

    private Guid? ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

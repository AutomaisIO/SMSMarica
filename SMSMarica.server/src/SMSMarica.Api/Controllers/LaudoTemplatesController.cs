using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.LaudoTemplates;
using SMSMarica.Core.LaudoTemplates.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// CRUD dos templates de laudo. Quem tem permissão pode criar templates
/// institucionais (mamografia, US, etc.) que ficam disponíveis para os
/// médicos como ponto de partida na hora de emitir um laudo.
/// </summary>
[ApiController]
[Route("laudo-templates")]
public sealed class LaudoTemplatesController(ILaudoTemplatesService service) : ControllerBase
{
    private readonly ILaudoTemplatesService _service = service;

    /// <summary>Lista templates (default: só ativos).</summary>
    /// <remarks>Leitura liberada para quem gere templates OU emite laudos (escolher o template).</remarks>
    [HttpGet]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.LaudosTemplates, ModuloPermissao.Laudos)]
    [ProducesResponseType<IReadOnlyList<LaudoTemplateListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<LaudoTemplateListItemDto>> Listar(
        [FromQuery] string? categoria,
        [FromQuery] bool incluirInativos = false,
        CancellationToken cancellationToken = default) =>
        await _service.ListarAsync(categoria, incluirInativos, cancellationToken);

    /// <summary>Detalhe de um template (incluindo conteúdo JSON/HTML e estrutura do checklist).</summary>
    [HttpGet("{id:guid}")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.LaudosTemplates, ModuloPermissao.Laudos)]
    [ProducesResponseType<LaudoTemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<LaudoTemplateDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    /// <summary>Cria um novo template.</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.LaudosTemplates, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarLaudoTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        var id = await _service.CadastrarAsync(usuarioId, request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    /// <summary>Atualiza um template existente.</summary>
    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.LaudosTemplates, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarLaudoTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        await _service.AtualizarAsync(id, usuarioId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Desativa (soft-delete). Laudos vinculados continuam intactos.</summary>
    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.LaudosTemplates, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Reativa um template antes desativado.</summary>
    [HttpPost("{id:guid}/reativar")]
    [RequerPermissao(ModuloPermissao.LaudosTemplates, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.ReativarAsync(id, cancellationToken);
        return NoContent();
    }

    private Guid ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id)) return id;
        throw new ValidacaoException("auth.sub_invalido", "Token sem identificação do usuário.");
    }
}

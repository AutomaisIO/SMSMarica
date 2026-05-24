using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Laudos;
using SMSMarica.Core.Laudos.Dtos;
using SMSMarica.Core.Laudos.Pdf;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Emissão e gestão de laudos PACS. Vinculados a um estudo DICOM pelo
/// <c>StudyInstanceUID</c>; assinatura é por snapshot de identificação do
/// médico (sem ICP-Brasil no MVP — ver tarja no PDF).
/// </summary>
[ApiController]
[Route("laudos")]
public sealed class LaudosController(ILaudosService service, ILaudoPdfRenderer pdf) : ControllerBase
{
    private readonly ILaudosService _service = service;
    private readonly ILaudoPdfRenderer _pdf = pdf;

    /// <summary>Lista laudos (filtros opcionais; default: últimos 50).</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<LaudoListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<LaudoListItemDto>> Listar(
        [FromQuery] string? studyInstanceUID,
        [FromQuery] Guid? pacienteId,
        [FromQuery] Guid? medicoId,
        [FromQuery] StatusLaudo? status,
        [FromQuery] DateOnly? dataInicial,
        [FromQuery] DateOnly? dataFinal,
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default) =>
        await _service.ListarAsync(
            new FiltroLaudosDto(studyInstanceUID, pacienteId, medicoId, status, dataInicial, dataFinal, limite),
            cancellationToken);

    /// <summary>
    /// Resolve laudos por uma lista de StudyInstanceUIDs (lookup batch).
    /// Usado pela listagem PACS para mostrar o botão "PDF" / "Editar".
    /// Query: <c>?studyUIDs=1.2,1.3,...</c> (CSV) ou repete <c>?studyUIDs=1.2&amp;studyUIDs=1.3</c>.
    /// </summary>
    [HttpGet("por-studies")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<LaudoPorStudyDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<LaudoPorStudyDto>> ListarPorStudies(
        [FromQuery(Name = "studyUIDs")] string[] studyUIDs,
        CancellationToken cancellationToken)
    {
        // Aceita também CSV (?studyUIDs=a,b,c).
        var lista = studyUIDs is null
            ? Array.Empty<string>()
            : [.. studyUIDs.SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))];
        return await _service.ListarPorStudyUidsAsync(lista, cancellationToken);
    }

    /// <summary>Última versão (rascunho ou finalizado) de laudo de um estudo.</summary>
    [HttpGet("por-estudo/{studyInstanceUID}")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Consulta)]
    [ProducesResponseType<LaudoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ObterPorStudy(
        string studyInstanceUID,
        CancellationToken cancellationToken)
    {
        var dto = await _service.ObterPorStudyAsync(studyInstanceUID, cancellationToken);
        return dto is null ? NoContent() : Ok(dto);
    }

    /// <summary>Detalhe do laudo (com conteúdo JSON+HTML).</summary>
    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Consulta)]
    [ProducesResponseType<LaudoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<LaudoDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    /// <summary>Histórico de versões do mesmo estudo (sem conteúdo).</summary>
    [HttpGet("{id:guid}/historico")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<LaudoHistoricoItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<LaudoHistoricoItemDto>> ListarHistorico(
        Guid id,
        CancellationToken cancellationToken) =>
        await _service.ListarHistoricoAsync(id, cancellationToken);

    /// <summary>Cria um rascunho para o estudo informado.</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarLaudoRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        var id = await _service.CadastrarAsync(usuarioId, request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    /// <summary>Atualiza um rascunho (409 se já finalizado).</summary>
    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarLaudoRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        await _service.AtualizarAsync(id, usuarioId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Finaliza um rascunho. Após isso ele se torna imutável; correções
    /// devem ir via <c>POST /{id}/nova-versao</c>.
    /// </summary>
    [HttpPost("{id:guid}/finalizar")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Finalizar(
        Guid id,
        [FromBody] FinalizarLaudoRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        await _service.FinalizarAsync(id, usuarioId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Cria nova versão (rascunho) a partir de um laudo finalizado.</summary>
    [HttpPost("{id:guid}/nova-versao")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Edicao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarNovaVersao(Guid id, CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        var novoId = await _service.CriarNovaVersaoAsync(id, usuarioId, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = novoId }, novoId);
    }

    /// <summary>Soft-delete (só rascunho).</summary>
    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        await _service.ExcluirAsync(id, usuarioId, cancellationToken);
        return NoContent();
    }

    /// <summary>PDF do laudo (cabeçalho + corpo + tarja CFM no rodapé).</summary>
    [HttpGet("{id:guid}/pdf")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Consulta)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var bytes = await _pdf.GerarAsync(id, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(bytes, "application/pdf", $"laudo-{id}.pdf");
    }

    private Guid ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id)) return id;
        throw new ValidacaoException("auth.sub_invalido", "Token sem identificação do usuário.");
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Laudos;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Laudos.Assinatura.Dtos;
using SMSMais.Core.Laudos.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Emissão e gestão de laudos PACS. Vinculados a um estudo DICOM pelo
/// <c>StudyInstanceUID</c>; assinatura é por snapshot de identificação do
/// médico (sem ICP-Brasil no MVP — ver tarja no PDF).
/// </summary>
[ApiController]
[Route("laudos")]
public sealed class LaudosController(ILaudosService service, ILaudoAssinaturaService assinatura) : ControllerBase
{
    private readonly ILaudosService _service = service;
    private readonly ILaudoAssinaturaService _assinatura = assinatura;

    /// <summary>Lista laudos (filtros opcionais; default: últimos 50).</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaLaudosDto>(StatusCodes.Status200OK)]
    public async Task<PaginaLaudosDto> Listar(
        [FromQuery] string? studyInstanceUID,
        [FromQuery] Guid? pacienteId,
        [FromQuery] Guid? medicoId,
        [FromQuery] StatusLaudo? status,
        [FromQuery] DateOnly? dataInicial,
        [FromQuery] DateOnly? dataFinal,
        [FromQuery] string? biRads,
        [FromQuery] bool? vinculado,
        [FromQuery] bool? assinado,
        [FromQuery] string? termo,
        [FromQuery] int limite = 50,
        [FromQuery] int pagina = 1,
        CancellationToken cancellationToken = default) =>
        await _service.ListarAsync(
            new FiltroLaudosDto(
                studyInstanceUID, pacienteId, medicoId, status, dataInicial, dataFinal, biRads,
                vinculado, assinado, termo, limite, pagina),
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

    /// <summary>
    /// PDF do laudo. Se já houver assinatura digital concluída, serve o PDF assinado
    /// (byte-estável); senão, gera on-demand com a tarja CFM no rodapé.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Consulta)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var download = await _assinatura.ObterPdfParaDownloadAsync(id, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        var nome = download.Assinado ? $"laudo-{id}-assinado.pdf" : $"laudo-{id}.pdf";
        return File(download.Conteudo, "application/pdf", nome);
    }

    /// <summary>
    /// PDF-base para o posicionamento do carimbo (ADR-0049): mesmo layout que será
    /// assinado, sem tarja/marca d'água. O painel o exibe para a médica arrastar/
    /// redimensionar o carimbo antes de disparar a assinatura.
    /// </summary>
    [HttpGet("{id:guid}/assinatura/pdf-base")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Edicao)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PdfBaseAssinatura(Guid id, CancellationToken cancellationToken)
    {
        var pdf = await _assinatura.ObterPdfBaseAsync(id, ExtrairUsuarioId(), cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(pdf, "application/pdf", $"laudo-{id}-base.pdf");
    }

    /// <summary>
    /// Inicia a assinatura digital de um laudo finalizado (médico autor). Cria o job,
    /// fixa a posição do carimbo escolhida no painel (ADR-0049) e devolve a chave de uso
    /// único — o front lança o agente via <c>automais-assinador://...?chave=</c>.
    /// </summary>
    [HttpPost("{id:guid}/assinatura/iniciar")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Edicao)]
    [ProducesResponseType<IniciarAssinaturaResultado>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IniciarAssinaturaResultado> IniciarAssinatura(
        Guid id, [FromBody] IniciarAssinaturaRequest? request, CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        return await _assinatura.IniciarAsync(id, usuarioId, request?.Posicao, cancellationToken);
    }

    /// <summary>Status da assinatura do laudo (para o front fazer polling após "Assinar").</summary>
    [HttpGet("{id:guid}/assinatura")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Consulta)]
    [ProducesResponseType<AssinaturaStatusDto>(StatusCodes.Status200OK)]
    public async Task<AssinaturaStatusDto> StatusAssinatura(Guid id, CancellationToken cancellationToken) =>
        await _assinatura.ObterStatusAsync(id, cancellationToken);

    /// <summary>PDF assinado aguardando a conferência do médico (preview do modal de aprovação).</summary>
    [HttpGet("{id:guid}/assinatura/pdf-aprovacao")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Edicao)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PdfAprovacao(Guid id, CancellationToken cancellationToken)
    {
        var pdf = await _assinatura.ObterPdfAprovacaoAsync(id, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(pdf, "application/pdf", $"laudo-{id}-conferencia.pdf");
    }

    /// <summary>Aprova o documento assinado após a conferência: oficializa e avisa o paciente.</summary>
    [HttpPost("{id:guid}/assinatura/aprovar")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AprovarAssinatura(Guid id, CancellationToken cancellationToken)
    {
        await _assinatura.AprovarAsync(id, ExtrairUsuarioId(), cancellationToken);
        return NoContent();
    }

    /// <summary>Rejeita o documento na conferência: cancela a assinatura e libera assinar de novo.</summary>
    [HttpPost("{id:guid}/assinatura/rejeitar")]
    [RequerPermissao(ModuloPermissao.Laudos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejeitarAssinatura(Guid id, CancellationToken cancellationToken)
    {
        await _assinatura.RejeitarAsync(id, ExtrairUsuarioId(), cancellationToken);
        return NoContent();
    }

    private Guid ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id)) return id;
        throw new ValidacaoException("auth.sub_invalido", "Token sem identificação do usuário.");
    }
}

/// <summary>
/// Corpo do "iniciar assinatura": posição do carimbo escolhida pela médica (ADR-0049).
/// Opcional — sem corpo/posição, mantém o padrão legado (rodapé da última página).
/// </summary>
public sealed record IniciarAssinaturaRequest(CarimboPosicaoDto? Posicao);

using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Cidadao;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Downloads;
using SMSMarica.Core.Exames;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Core.SolicitacoesExame.Declaracao;
using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Solicitações de exame que geram worklist DICOM (MWL) no dcm4chee. Ciclo:
/// Solicitada → Enviada → Recebida → Agendada → EmExecucao → Realizada → Laudada.
/// </summary>
[ApiController]
[Route("solicitacoes-exame")]
public sealed class SolicitacoesExameController(
    ISolicitacoesExameService service,
    IDeclaracaoComparecimentoService declaracao,
    IExameCompletoPdfService exameCompleto,
    IDownloadTokenService downloads,
    ICidadaoLoginLinkService loginLinks,
    IBackfillDataEstudoService backfill) : ControllerBase
{
    private readonly ISolicitacoesExameService _service = service;
    private readonly IDeclaracaoComparecimentoService _declaracao = declaracao;
    private readonly IExameCompletoPdfService _exameCompleto = exameCompleto;
    private readonly IDownloadTokenService _downloads = downloads;
    private readonly ICidadaoLoginLinkService _loginLinks = loginLinks;
    private readonly IBackfillDataEstudoService _backfill = backfill;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SolicitacaoExameListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<SolicitacaoExameListItemDto>> Listar(
        [FromQuery] StatusSolicitacaoExame? status,
        [FromQuery] Guid? pacienteId,
        [FromQuery] Guid? unidadeId,
        [FromQuery] Guid? tipoExameId,
        [FromQuery] DateOnly? dataInicial,
        [FromQuery] DateOnly? dataFinal,
        [FromQuery] string? accessionNumber,
        [FromQuery] string? busca,
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default) =>
        await _service.ListarAsync(
            new FiltroSolicitacoesDto(status, pacienteId, unidadeId, tipoExameId, dataInicial, dataFinal, accessionNumber, busca, limite),
            cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<SolicitacaoExameDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<SolicitacaoExameDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpGet("por-accession/{accession}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<SolicitacaoExameDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ObterPorAccession(string accession, CancellationToken cancellationToken)
    {
        var dto = await _service.ObterPorAccessionAsync(accession, cancellationToken);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpGet("por-study/{studyInstanceUID}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<SolicitacaoExameDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ObterPorStudy(string studyInstanceUID, CancellationToken cancellationToken)
    {
        var dto = await _service.ObterPorStudyAsync(studyInstanceUID, cancellationToken);
        return dto is null ? NoContent() : Ok(dto);
    }

    /// <summary>
    /// PDF da declaração de comparecimento (A5, 148×200mm) — só disponível para
    /// solicitações já realizadas (ou laudadas). Usa a data/hora real do estudo no
    /// PACS e a imagem do cabeçalho do laudo; assinada pelo usuário que a emitiu.
    /// </summary>
    [HttpGet("{id:guid}/declaracao-comparecimento")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeclaracaoComparecimento(
        Guid id,
        [FromQuery] DateTime? horaEntrada,
        [FromQuery] DateTime? horaSaida,
        [FromQuery] string? motivo,
        CancellationToken cancellationToken)
    {
        var parametros = new DeclaracaoComparecimentoParametros(horaEntrada, horaSaida, motivo);
        var pdf = await _declaracao.GerarAsync(id, parametros, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(pdf, "application/pdf", $"declaracao-comparecimento-{id}.pdf");
    }

    /// <summary>
    /// PDF do EXAME COMPLETO: capa com os dados da solicitação, as imagens do PACS e,
    /// por último, o laudo (quando finalizado) — tudo num único documento.
    /// </summary>
    [HttpGet("{id:guid}/exame-completo-pdf")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExameCompletoPdf(Guid id, CancellationToken cancellationToken)
    {
        var pdf = await _exameCompleto.GerarAsync(id, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(pdf, "application/pdf", $"exame-completo-{id}.pdf");
    }

    /// <summary>
    /// Gera um link público de download (uso único, validade configurável) do exame
    /// completo, para enviar ao paciente (ex.: WhatsApp). Retorna a URL e a expiração.
    /// </summary>
    [HttpPost("{id:guid}/link-download")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<DownloadLinkDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<DownloadLinkDto> GerarLinkDownload(Guid id, CancellationToken cancellationToken) =>
        await _downloads.GerarExameCompletoAsync(id, cancellationToken);

    /// <summary>
    /// Gera um "magic-link" de acesso (login em 1 clique) do paciente da solicitação,
    /// para enviar por WhatsApp. Uso único, validade configurável (Config de Laudo).
    /// </summary>
    [HttpPost("{id:guid}/link-acesso")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<MagicLinkDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<MagicLinkDto> GerarLinkAcesso(Guid id, CancellationToken cancellationToken) =>
        await _loginLinks.GerarParaSolicitacaoAsync(id, cancellationToken: cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarSolicitacaoExameRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarSolicitacaoExameRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancelar")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(
        Guid id,
        [FromBody] CancelarSolicitacaoExameRequest request,
        CancellationToken cancellationToken)
    {
        await _service.CancelarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reenviar-worklist")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReenviarWorklist(Guid id, CancellationToken cancellationToken)
    {
        await _service.ReenviarWorklistAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Exclui a solicitação. Requer permissão de Exclusão (concedida apenas a perfis
    /// administrativos). Por padrão remove primeiro o item de worklist no dcm4chee e
    /// confirma (anti-lixo); se o PACS recusar/cair, responde 409
    /// (<c>solicitacaoExame.exclusao_pacs_falhou</c>) e nada é apagado. Com
    /// <c>force=true</c>, ignora o PACS e limpa só a base local.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Excluir(Guid id, [FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        await _service.ExcluirAsync(id, force, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Manutenção: preenche <c>data_estudo</c> (data DICOM) nos exames antigos que
    /// ficaram com o campo nulo (realizados antes de a coluna existir). Idempotente,
    /// em lote (<c>limite</c>). <c>dryRun=true</c> só conta os candidatos — não toca
    /// no PACS nem no banco.
    /// </summary>
    [HttpPost("backfill-data-estudo")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType<BackfillDataEstudoResultado>(StatusCodes.Status200OK)]
    public async Task<BackfillDataEstudoResultado> BackfillDataEstudo(
        [FromQuery] int limite = 500,
        [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
        => await _backfill.ExecutarAsync(limite, dryRun, cancellationToken);
}

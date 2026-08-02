using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Cidadao;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Downloads;
using SMSMarica.Core.Exames;
using SMSMarica.Core.Integracoes.SisregWeb.Importacao;
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
    ISolicitacaoHistoricoService historico,
    IBackfillDataEstudoService backfill,
    IImportacaoSisregService importacao) : ControllerBase
{
    private readonly ISolicitacoesExameService _service = service;
    private readonly IDeclaracaoComparecimentoService _declaracao = declaracao;
    private readonly IExameCompletoPdfService _exameCompleto = exameCompleto;
    private readonly IDownloadTokenService _downloads = downloads;
    private readonly ICidadaoLoginLinkService _loginLinks = loginLinks;
    private readonly ISolicitacaoHistoricoService _historico = historico;
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
        /// <summary>Recorte do painel de início ("ver todos" de uma raia) — ADR-0033.</summary>
        [FromQuery] RecortePainel? painel = null,
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default) =>
        await _service.ListarAsync(
            new FiltroSolicitacoesDto(
                status, pacienteId, unidadeId, tipoExameId, dataInicial, dataFinal,
                accessionNumber, busca, painel, limite),
            cancellationToken);

    /// <summary>
    /// Pendências de importação que casam com o termo buscado — o bloco exibido ACIMA da lista para
    /// a recepção achar quem chegou e "não tem agendamento" (ADR-0035). São linhas do SISREG que
    /// NÃO entraram no sistema: nunca devem ser confundidas com solicitações reais.
    ///
    /// <b>Gateado por <c>SolicitacoesExame/Consulta</c>, não por <c>Sisreg</c></b>: quem precisa
    /// disto é a recepção, que quase nunca tem permissão do módulo SISREG. É a mesma exceção que o
    /// ADR-0029 abriu para <c>GET /unidades/atendimento</c> — gatear pela permissão do módulo dono
    /// do dado, e não pela da tela que consome, sobe a feature quebrada.
    /// </summary>
    [HttpGet("pendencias-importacao")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ImportacaoFalhaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ImportacaoFalhaDto>> PendenciasImportacao(
        [FromQuery] string? busca,
        [FromQuery] int limite = 5,
        CancellationToken cancellationToken = default)
        => await importacao.BuscarPendenciasPorPacienteAsync(
            busca ?? string.Empty, limite is <= 0 or > 25 ? 5 : limite, cancellationToken);

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

    /// <summary>Histórico do processo de comunicação (comunicações WhatsApp + contatos manuais).</summary>
    [HttpGet("{id:guid}/historico")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<HistoricoSolicitacaoDto>(StatusCodes.Status200OK)]
    public async Task<HistoricoSolicitacaoDto> Historico(Guid id, CancellationToken cancellationToken) =>
        await _historico.ObterAsync(id, cancellationToken);

    /// <summary>
    /// Reenvia uma comunicação (WhatsApp): REVOGA todos os links de acesso anteriores da
    /// solicitação — e as sessões abertas por eles, se usados — e reconstrói o envio com os
    /// dados ATUAIS do paciente (telefone certo, link novo). Para casos de envio errado
    /// (ex.: telefone trocado entre pacientes).
    /// </summary>
    [HttpPost("{id:guid}/comunicacoes/{comunicacaoId:guid}/reenviar")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReenviarComunicacao(
        Guid id, Guid comunicacaoId,
        [FromServices] Core.Notificacoes.Comunicacao.IComunicacaoPacienteService comunicacoes,
        CancellationToken cancellationToken)
    {
        await comunicacoes.ReenviarAsync(id, comunicacaoId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Envio MANUAL do resultado ao paciente: cria/reconstrói a comunicação da finalidade
    /// (ExameLiberado ou LaudoPronto) e dispara na hora, registrando quem enviou. Com
    /// <c>assumirRisco=true</c> envia mesmo sem telefone verificado.
    /// </summary>
    [HttpPost("{id:guid}/enviar-comunicacao")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnviarComunicacaoManual(
        Guid id, [FromBody] EnviarComunicacaoManualRequest request, CancellationToken cancellationToken)
    {
        await _service.EnviarComunicacaoManualAsync(
            id, request.Finalidade, request.AssumirRisco, cancellationToken);
        return NoContent();
    }

    /// <summary>Registra um contato MANUAL com o paciente ("liguei, não atendeu"...). Append-only.</summary>
    [HttpPost("{id:guid}/contatos")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarContato(
        Guid id, [FromBody] RegistrarContatoRequest request, CancellationToken cancellationToken)
    {
        await _historico.RegistrarContatoAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Autorização presencial (recepção): grava a chave e libera o envio ao PACS.
    /// Exige paciente com número verificado.</summary>
    [HttpPost("{id:guid}/autorizar")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Autorizar(
        Guid id,
        [FromBody] AutorizarSolicitacaoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AutorizarAsync(id, request.ChaveConfirmacao, request.EquipamentoId, cancellationToken);
        return NoContent();
    }

    /// <summary><paramref name="EquipamentoId"/> = estação escolhida. Obrigatório quando a unidade
    /// tem mais de um equipamento na modalidade (senão a autorização recusa pedindo a seleção).</summary>
    public sealed record AutorizarSolicitacaoRequest(string ChaveConfirmacao, Guid? EquipamentoId = null);

    /// <summary>Equipamentos elegíveis para executar o exame — a tela de autorização usa para
    /// montar a seleção da estação quando há mais de um.</summary>
    [HttpGet("{id:guid}/equipamentos")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<EquipamentoExameDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<EquipamentoExameDto>> EquipamentosDisponiveis(
        Guid id, CancellationToken cancellationToken) =>
        await _service.ListarEquipamentosDisponiveisAsync(id, cancellationToken);

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

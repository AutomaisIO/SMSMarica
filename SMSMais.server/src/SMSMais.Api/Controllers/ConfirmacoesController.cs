using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Comunicacao.Dtos;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Menu Confirmações: a fila de confirmações de agendamento por WhatsApp (o que está empilhado
/// esperando o horário, retido, enviado), as respostas dos pacientes e as regras de disparo.
/// A resposta do paciente vive só neste sistema — nada daqui é escrito no SISREG.
/// </summary>
[ApiController]
[Route("confirmacoes")]
public sealed class ConfirmacoesController(
    IConfirmacaoConfiguracaoService configuracao,
    IConfirmacoesPainelService painel,
    ILoteConfirmacaoService lote,
    IComunicacaoGestaoService gestao,
    IAtendimentoConfirmacaoService atendimento,
    IConfirmacoesEquipeService equipe) : ControllerBase
{
    // ---- Aba Equipe (módulo próprio: quem atende não vê o ranking das colegas) ----

    /// <summary>Produção por atendente no período (dias de Brasília). Sem datas, os últimos 7 dias.</summary>
    [HttpGet("equipe")]
    [RequerPermissao(ModuloPermissao.ConfirmacoesEquipe, AcoesPermissao.Consulta)]
    [ProducesResponseType<EquipeConfirmacoesDto>(StatusCodes.Status200OK)]
    public async Task<EquipeConfirmacoesDto> Equipe(
        [FromQuery] DateOnly? de = null, [FromQuery] DateOnly? ate = null, CancellationToken ct = default)
    {
        var (inicio, fim) = PeriodoPadrao(de, ate);
        return await equipe.ObterAsync(inicio, fim, ct);
    }

    /// <summary>Os atos de uma atendente no período (linha do tempo dela).</summary>
    [HttpGet("equipe/{usuarioId:guid}/atos")]
    [RequerPermissao(ModuloPermissao.ConfirmacoesEquipe, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AtoAtendenteDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AtoAtendenteDto>> AtosDaAtendente(
        Guid usuarioId, [FromQuery] DateOnly? de = null, [FromQuery] DateOnly? ate = null, CancellationToken ct = default)
    {
        var (inicio, fim) = PeriodoPadrao(de, ate);
        return await equipe.AtosAsync(usuarioId, inicio, fim, ct);
    }

    private static (DateOnly De, DateOnly Ate) PeriodoPadrao(DateOnly? de, DateOnly? ate)
    {
        var fim = ate ?? DateOnly.FromDateTime(Core.Common.Tempo.FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        return (de ?? fim.AddDays(-6), fim);
    }

    // ---- Atendimento humano (as 4 abas do menu) ----

    /// <summary>Uma das quatro filas: NaoConfirmados | Confirmados | ContatoErrado | Pendentes.
    /// <paramref name="envio"/> filtra pela situação do envio automático (status ou "NaoEnviada").</summary>
    [HttpGet("atendimento")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaAtendimentoDto>(StatusCodes.Status200OK)]
    public async Task<PaginaAtendimentoDto> Atendimento(
        [FromQuery] AbaAtendimentoConfirmacao aba = AbaAtendimentoConfirmacao.NaoConfirmados,
        [FromQuery] string? texto = null,
        [FromQuery] Guid? unidadeId = null,
        [FromQuery] string? envio = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50,
        CancellationToken ct = default) =>
        await atendimento.ListarAsync(aba, texto, unidadeId, envio, pagina, tamanho, ct);

    [HttpGet("atendimento/resumo")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<ResumoAbasAtendimentoDto>(StatusCodes.Status200OK)]
    public async Task<ResumoAbasAtendimentoDto> ResumoAtendimento(CancellationToken ct) =>
        await atendimento.ResumoAsync(ct);

    /// <summary>Os porquês da aba Telefone comprometido (sem celular × não é WhatsApp).</summary>
    [HttpGet("atendimento/telefone-comprometido/motivos")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<MotivosTelefoneComprometidoDto>(StatusCodes.Status200OK)]
    public async Task<MotivosTelefoneComprometidoDto> MotivosTelefoneComprometido(CancellationToken ct) =>
        await atendimento.MotivosTelefoneComprometidoAsync(ct);

    /// <summary>Contexto da conversa com o paciente (aba Cancelamento).</summary>
    [HttpGet("atendimento/{solicitacaoId:guid}/conversa")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<MensagemContextoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<MensagemContextoDto>> ConversaAtendimento(
        Guid solicitacaoId, CancellationToken ct, [FromQuery] int quantas = 30) =>
        await atendimento.ConversaAsync(solicitacaoId, quantas, ct);

    [HttpGet("atendimento/atendentes")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AtendenteConfirmacaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AtendenteConfirmacaoDto>> Atendentes(CancellationToken ct) =>
        await atendimento.ListarAtendentesAsync(ct);

    [HttpGet("atendimento/{solicitacaoId:guid}/historico")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<EventoAtendimentoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<EventoAtendimentoDto>> HistoricoAtendimento(Guid solicitacaoId, CancellationToken ct) =>
        await atendimento.HistoricoAsync(solicitacaoId, ct);

    /// <summary>Começa a atender (ou retoma uma estacionada). Encerra o envio automático que ainda não saiu.</summary>
    [HttpPost("atendimento/{solicitacaoId:guid}/atender")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<AcaoAtendimentoResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<AcaoAtendimentoResultadoDto> Atender(Guid solicitacaoId, CancellationToken ct) =>
        await atendimento.AtenderAsync(solicitacaoId, ct);

    /// <summary>Pega para si um atendimento que está com outra pessoa.</summary>
    [HttpPost("atendimento/{solicitacaoId:guid}/assumir")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<AcaoAtendimentoResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<AcaoAtendimentoResultadoDto> Assumir(Guid solicitacaoId, CancellationToken ct) =>
        await atendimento.AssumirAsync(solicitacaoId, ct);

    [HttpPost("atendimento/{solicitacaoId:guid}/transferir")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<AcaoAtendimentoResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<AcaoAtendimentoResultadoDto> Transferir(
        Guid solicitacaoId, [FromBody] TransferirAtendimentoRequest request, CancellationToken ct) =>
        await atendimento.TransferirAsync(solicitacaoId, request, ct);

    /// <summary>Devolve à fila sem desfecho.</summary>
    [HttpPost("atendimento/{solicitacaoId:guid}/liberar")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<AcaoAtendimentoResultadoDto>(StatusCodes.Status200OK)]
    public async Task<AcaoAtendimentoResultadoDto> Liberar(Guid solicitacaoId, CancellationToken ct) =>
        await atendimento.LiberarAsync(solicitacaoId, ct);

    /// <summary>Confirma a presença pela mão da atendente (canal "atendente").</summary>
    [HttpPost("atendimento/{solicitacaoId:guid}/confirmar")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<AcaoAtendimentoResultadoDto>(StatusCodes.Status200OK)]
    public async Task<AcaoAtendimentoResultadoDto> Confirmar(
        Guid solicitacaoId, [FromBody] ConfirmarAtendimentoRequest request, CancellationToken ct) =>
        await atendimento.ConfirmarAsync(solicitacaoId, request, ct);

    /// <summary>
    /// Cancela o agendamento — <b>no SISREG primeiro</b>, assinado com o login do operador
    /// (<c>POST /sisreg/sessao</c>), e só aqui se a ficha de lá confirmar. Se o SISREG não
    /// confirmar, responde 409 e <b>nada muda</b>: a vaga continua de pé nos dois sistemas.
    /// </summary>
    [HttpPost("atendimento/{solicitacaoId:guid}/cancelar")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Exclusao)]
    [ProducesResponseType<AcaoAtendimentoResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<AcaoAtendimentoResultadoDto> Cancelar(
        Guid solicitacaoId, [FromBody] CancelarAtendimentoRequest request, CancellationToken ct) =>
        await atendimento.CancelarAsync(solicitacaoId, request, ct);

    /// <summary>Desfaz um pedido de cancelamento registrado por engano — a ficha volta para a fila
    /// de confirmação. Não serve para agendamento já cancelado de verdade.</summary>
    [HttpPost("atendimento/{solicitacaoId:guid}/desfazer-pedido-cancelamento")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<AcaoAtendimentoResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<AcaoAtendimentoResultadoDto> DesfazerPedidoCancelamento(
        Guid solicitacaoId, [FromBody] DesfazerPedidoCancelamentoRequest request, CancellationToken ct) =>
        await atendimento.DesfazerPedidoCancelamentoAsync(solicitacaoId, request, ct);

    [HttpPost("atendimento/{solicitacaoId:guid}/pendente")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<AcaoAtendimentoResultadoDto>(StatusCodes.Status200OK)]
    public async Task<AcaoAtendimentoResultadoDto> Pendente(
        Guid solicitacaoId, [FromBody] PendenteAtendimentoRequest request, CancellationToken ct) =>
        await atendimento.EnviarParaPendenteAsync(solicitacaoId, request, ct);

    [HttpPost("atendimento/{solicitacaoId:guid}/contato-errado")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<AcaoAtendimentoResultadoDto>(StatusCodes.Status200OK)]
    public async Task<AcaoAtendimentoResultadoDto> ContatoErrado(
        Guid solicitacaoId, [FromBody] ContatoErradoAtendimentoRequest request, CancellationToken ct) =>
        await atendimento.ContatoErradoAsync(solicitacaoId, request, ct);

    /// <summary>Depois de gravar/verificar o telefone novo: fecha a pendência e devolve à fila automática.</summary>
    [HttpPost("atendimento/{solicitacaoId:guid}/contato-corrigido")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<AcaoAtendimentoResultadoDto>(StatusCodes.Status200OK)]
    public async Task<AcaoAtendimentoResultadoDto> ContatoCorrigido(
        Guid solicitacaoId, [FromBody] ContatoCorrigidoAtendimentoRequest request, CancellationToken ct) =>
        await atendimento.ContatoCorrigidoAsync(solicitacaoId, request, ct);

    // ---- Fila ----

    [HttpGet("fila/resumo")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Confirmacoes, ModuloPermissao.NotificacoesAgendamento)]
    [ProducesResponseType<ResumoFilaConfirmacaoDto>(StatusCodes.Status200OK)]
    public async Task<ResumoFilaConfirmacaoDto> ResumoFila(CancellationToken ct) =>
        await painel.ResumoFilaAsync(ct);

    /// <summary>Lista da fila — só a finalidade confirmação de agendamento.</summary>
    [HttpGet("fila")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Confirmacoes, ModuloPermissao.NotificacoesAgendamento)]
    [ProducesResponseType<PaginaComunicacoesDto>(StatusCodes.Status200OK)]
    public async Task<PaginaComunicacoesDto> Fila(
        [FromQuery] string? status,
        [FromQuery] string? confirmacao,
        [FromQuery] string? texto,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50,
        CancellationToken ct = default) =>
        await gestao.ListarAsync(new ComunicacaoFiltroDto(
            status, nameof(FinalidadeComunicacao.ConfirmacaoAgendamento), confirmacao, texto, de, ate, pagina, tamanho), ct);

    [HttpGet("fila/{id:guid}")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Confirmacoes, ModuloPermissao.NotificacoesAgendamento)]
    [ProducesResponseType<ComunicacaoDetalheDto>(StatusCodes.Status200OK)]
    public async Task<ComunicacaoDetalheDto> ObterDaFila(Guid id, CancellationToken ct) =>
        await gestao.ObterAsync(id, ct);

    /// <summary>Recoloca na fila (respeita a janela de horário: fora dela, sai quando abrir).</summary>
    [HttpPost("fila/{id:guid}/reenviar")]
    [RequerQualquerPermissao(AcoesPermissao.Edicao, ModuloPermissao.Confirmacoes, ModuloPermissao.NotificacoesAgendamento)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reenviar(Guid id, CancellationToken ct)
    {
        await gestao.ReenviarAsync(id, ct);
        return NoContent();
    }

    // ---- Respostas dos pacientes ----

    /// <summary>Quem confirmou e quem avisou que não vai (com o motivo). <paramref name="resposta"/>:
    /// Confirmada | Cancelada (vazio = as duas). Período sobre a data da resposta.</summary>
    [HttpGet("respostas")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Confirmacoes, ModuloPermissao.NotificacoesAgendamento)]
    [ProducesResponseType<PaginaRespostasConfirmacaoDto>(StatusCodes.Status200OK)]
    public async Task<PaginaRespostasConfirmacaoDto> Respostas(
        [FromQuery] string? resposta,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] string? texto,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50,
        CancellationToken ct = default) =>
        await painel.ListarRespostasAsync(resposta, de, ate, texto, pagina, tamanho, ct);

    // ---- Disparo em lote (estoque que a importação deixou para trás) ----

    /// <summary>Prévia do lote: quantos seriam avisados, por dia, e quantos ficam de fora e por quê.
    /// Não muda nada. <paramref name="de"/>/<paramref name="ate"/> são dias de Brasília, inclusivos
    /// (é como se deixa o dia seguinte de fora e começa na segunda).</summary>
    [HttpGet("lote/previa")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Confirmacoes, ModuloPermissao.NotificacoesAgendamento)]
    [ProducesResponseType<PreviaLoteConfirmacaoDto>(StatusCodes.Status200OK)]
    public async Task<PreviaLoteConfirmacaoDto> PreviaLote(
        [FromQuery] Guid? unidadeId, [FromQuery] DateOnly? de, [FromQuery] DateOnly? ate,
        [FromQuery] bool forcar = false, [FromQuery] bool incluirJaAvisados = false,
        [FromQuery] bool incluirJaConfirmados = false, CancellationToken ct = default) =>
        await lote.PreverAsync(unidadeId, de, ate, forcar, incluirJaAvisados, incluirJaConfirmados, ct);

    /// <param name="Forcar">Ignora as chaves de unidade/procedimento (exige unidade escolhida).</param>
    /// <param name="IgnorarJanela">Este lote sai agora, mesmo fora do horário de envio.</param>
    /// <param name="IncluirJaAvisados">Reenvia para quem já recebeu (rearma a comunicação).</param>
    /// <param name="IncluirJaConfirmados">Reenvia para quem já confirmou (a resposta volta a Pendente).</param>
    public sealed record DispararLoteRequest(
        Guid? UnidadeId, DateOnly? De, DateOnly? Ate, bool Forcar = false, bool IgnorarJanela = false,
        bool IncluirJaAvisados = false, bool IncluirJaConfirmados = false);

    /// <summary>Enfileira o lote (o worker envia, respeitando janela e vazão). Idempotente.</summary>
    [HttpPost("lote")]
    [RequerQualquerPermissao(AcoesPermissao.Edicao, ModuloPermissao.Confirmacoes, ModuloPermissao.NotificacoesAgendamento)]
    [ProducesResponseType<PreviaLoteConfirmacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<PreviaLoteConfirmacaoDto> DispararLote(
        [FromBody] DispararLoteRequest request, CancellationToken ct) =>
        await lote.DispararAsync(
            request.UnidadeId, request.De, request.Ate, request.Forcar, request.IgnorarJanela,
            request.IncluirJaAvisados, request.IncluirJaConfirmados, ct);

    // ---- Regras ----

    [HttpGet("configuracao")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Confirmacoes, ModuloPermissao.NotificacoesAgendamento)]
    [ProducesResponseType<ConfirmacaoConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<ConfirmacaoConfiguracaoDto> ObterConfiguracao(CancellationToken ct) =>
        await configuracao.ObterAsync(ct);

    [HttpPut("configuracao")]
    [RequerQualquerPermissao(AcoesPermissao.Edicao, ModuloPermissao.Confirmacoes, ModuloPermissao.NotificacoesAgendamento)]
    [ProducesResponseType<ConfirmacaoConfiguracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ConfirmacaoConfiguracaoDto> SalvarConfiguracao(
        [FromBody] SalvarConfirmacaoConfiguracaoRequest request, CancellationToken ct) =>
        await configuracao.SalvarAsync(request, ct);

    [HttpGet("regras/unidades")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Confirmacoes, ModuloPermissao.NotificacoesAgendamento)]
    [ProducesResponseType<IReadOnlyList<RegraUnidadeConfirmacaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<RegraUnidadeConfirmacaoDto>> RegrasUnidades(CancellationToken ct) =>
        await painel.ListarRegrasUnidadesAsync(ct);

    public sealed record AlterarRegraUnidadeRequest(bool Enviar);

    [HttpPut("regras/unidades/{unidadeId:guid}")]
    [RequerQualquerPermissao(AcoesPermissao.Edicao, ModuloPermissao.Confirmacoes, ModuloPermissao.NotificacoesAgendamento)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AlterarRegraUnidade(
        Guid unidadeId, [FromBody] AlterarRegraUnidadeRequest request, CancellationToken ct)
    {
        await painel.AlterarRegraUnidadeAsync(unidadeId, request.Enviar, ct);
        return NoContent();
    }
}

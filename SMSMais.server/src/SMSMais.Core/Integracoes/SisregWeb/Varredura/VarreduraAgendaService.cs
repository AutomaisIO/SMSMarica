using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Background;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Varredura;

public interface IVarreduraAgendaService
{
    Task<VarreduraAgendaDto> ObterAgendaAsync(CancellationToken ct);

    Task<VarreduraAgendaDto> SalvarAgendaAsync(SalvarVarreduraAgendaRequest request, CancellationToken ct);

    /// <summary>Dispara a varredura da unidade ativa, agora. ESCRITA no SISREG? Não — só leitura.</summary>
    Task<VarreduraAceitaDto> IniciarAsync(CancellationToken ct);

    /// <summary>Dispara uma varredura MANUAL por período específico (inclusive datas passadas). Não
    /// avisa o paciente por WhatsApp — é backfill.</summary>
    Task<VarreduraAceitaDto> IniciarPeriodoAsync(IniciarVarreduraPeriodoRequest request, CancellationToken ct);

    /// <summary>Disparo pelo scheduler. Devolve null em colisão (não é erro: só reprograma).</summary>
    Task<Guid?> IniciarAgendadoAsync(Guid unidadeId, CancellationToken ct);

    /// <summary>
    /// Um período específico de uma unidade EXPLÍCITA, sem depender do header <c>X-Unidade-Id</c>.
    ///
    /// <para>Existe para o motor de histórico, que roda em background e por isso não tem request de
    /// onde tirar a unidade. É o mesmo caminho do backfill manual — inclusive a supressão do aviso
    /// ao paciente, que é obrigatória aqui: importar agenda de meses atrás não pode disparar
    /// WhatsApp sobre consulta que já aconteceu.</para>
    ///
    /// <para>Devolve null em colisão ou fora da janela de entrada — como o disparo agendado, não é
    /// erro: o motor só tenta de novo no próximo tick.</para>
    /// </summary>
    Task<Guid?> IniciarPeriodoAgendadoAsync(
        Guid unidadeId, DateOnly inicio, DateOnly fim, CancellationToken ct);

    /// <summary>Liga/desliga a importação do passado da unidade ativa.</summary>
    Task<VarreduraAgendaDto> AlternarHistoricoAsync(AlternarHistoricoRequest request, CancellationToken ct);

    /// <summary>Executa o job — chamado pelo runner, fora de qualquer request.</summary>
    Task ExecutarAsync(VarreduraJob job, CancellationToken ct);

    Task<StatusVarreduraVivo?> ObterStatusAsync(CancellationToken ct);

    bool Cancelar();

    Task<IReadOnlyList<VarreduraExecucaoDto>> ListarExecucoesAsync(int limite, CancellationToken ct);

    /// <summary>Detalhe por profissional × procedimento de UMA execução (o modal do histórico).</summary>
    Task<IReadOnlyList<VarreduraExecucaoItemDto>> ListarItensAsync(Guid execucaoId, CancellationToken ct);
}

/// <summary>
/// Varre a agenda do SISREG de uma unidade exportando o arquivo de agendamentos
/// (<c>expo_solicitacoes</c>) e entrega as marcações ao mesmo fluxo de importação do upload manual.
///
/// <para><b>Por que a exportação e não a raspagem da tela de agenda</b> (medido em 03/08/2026, no
/// CDT, mesmo par profissional × procedimento, 419 registros em julho): a exportação custa <b>1
/// requisição</b> contra <b>9</b> da tela paginada de 50 em 50 — e o orçamento de requisições é o
/// recurso escasso aqui. Além disso o arquivo traz o <b>código SIGTAP</b> (coluna 2), as datas de
/// solicitação e regulação e o endereço do paciente, que a tela de agenda simplesmente não
/// informa. E reusa o <see cref="AgendaTxtParser"/>, que já roda em produção.</para>
///
/// <para><b>Por que o produto cartesiano:</b> a exportação exige profissional e procedimento, como
/// a tela de agenda. Logo, varrer = percorrer os pares habilitados, e é por isso que os checkboxes
/// do mapeamento são a régua de custo.</para>
///
/// <para><b>Somente leitura:</b> <c>etapa=exportar</c> apenas gera o arquivo. Confirmar presença ou
/// registrar falta escreveria na agenda de verdade e não acontece em lugar nenhum daqui.</para>
///
/// <para><b>Bloqueio de horário:</b> o <c>expo_solicitacoes</c> é bloqueado pelo SISREG das 8h às
/// 15h. A janela de execução (22:00–06:00) já respeita isso — mas aqui isso deixa de ser só
/// cortesia com o operador humano e passa a ser requisito do próprio recurso.</para>
/// </summary>
public sealed class VarreduraAgendaService(
    SmsMaisDbContext db,
    // Contexto próprio para o mapeamento observado — ver AtualizarMapeamentoObservadoAsync.
    IDbContextFactory<SmsMaisDbContext> dbFactory,
    ISisregWebSessao sessao,
    ISisregUnidadeAtual unidadeAtual,
    IImportacaoSisregService importacao,
    // Só para o passo FHIR ao fim da varredura. Fala com o hub, não com o SISREG — não consome
    // orçamento anti-robô.
    Mapeamento.ISisregMapeamentoService mapeamento,
    Cadastro.IPreCargaCadastroSerService preCarga,
    IVarreduraSisregFila fila,
    VarreduraSisregEstadoVivo estadoVivo,
    Importacao.Background.SisregImportacaoEstadoVivo importacaoEstadoVivo,
    MapeamentoLote.Background.MapeamentoLoteEstadoVivo loteEstadoVivo,
    IUsuarioAtualAccessor usuarioAtual,
    IOptions<VarreduraSisregOpcoes> opcoes,
    ILogger<VarreduraAgendaService> logger) : IVarreduraAgendaService
{
    private const string Caminho = "/cgi-bin/expo_solicitacoes";

    /// <summary>Teto de registros por exportação, medido no SISREG. Ver ExportarComTetoAsync.</summary>
    private const int TetoRegistrosPorExportacao = 700;

    /// <summary>
    /// O valor que o formulário do <c>expo_solicitacoes</c> usa para "não escolhi" — a
    /// option-sentinela <c>Selecione o Profissional</c> / <c>Selecione o Procedimento</c>.
    ///
    /// <para>Mandá-lo nos dois campos devolve a agenda da UNIDADE INTEIRA. Medido em 27/08/2026 no
    /// CDT, janela de 29 dias: 3.286 linhas e 65 procedimentos numa requisição, contendo
    /// integralmente o recorte por par (os 320 registros do controle estavam todos lá). O JS do
    /// formulário só valida as datas e o intervalo de 31 dias — nunca estes dois campos.</para>
    ///
    /// <para><b>Não vale para o <c>cons_agendas</c></b>, onde o mesmo experimento falha: lá os três
    /// filtros são obrigatórios no servidor. Telas diferentes, regras diferentes.</para>
    /// </summary>
    private const string SemFiltro = "0";

    /// <summary>
    /// Quantas marcações importar entre duas gravações de progresso, no recorte por unidade
    /// inteira. Nada a ver com requisição ao SISREG — é só o ritmo do feedback.
    ///
    /// <para>Era 100 e ficou pequeno demais na prática: cada paciente NOVO custa uns 3 segundos
    /// (a consulta de cadastro ao SER), então o primeiro número só mudava depois de uns cinco
    /// minutos e a tela parecia travada em "0 importadas". Com 20, o operador vê movimento a cada
    /// minuto. O custo é um SaveChanges a mais por lote, que não é nada perto disso.</para>
    /// </summary>
    /// <summary>
    /// Proporção de ausentes acima da qual a leitura é considerada incompleta, não cancelamento.
    /// Um quinto da agenda sumir de uma vez é evento raríssimo; exportação truncada, não.
    /// </summary>
    /// <summary>
    /// Rede de segurança para unidade SEM escala cadastrada: quantos dias varrer quando não há de
    /// onde derivar a janela. Constante de propósito — a janela deixou de ser escolha em 05/09/2026
    /// (ver <c>UltimoDiaDeAgendaAsync</c>). A coluna <c>dias_a_frente</c> continua no banco porque
    /// migration é imutável, mas nada mais a lê para decidir.
    /// </summary>
    private const int PisoDiasAFrente = 21;

    private const double LimiteAusentesSuspeito = 0.20;

    private const int LoteDeImportacao = 20;
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly VarreduraSisregOpcoes _opcoes = opcoes.Value;

    /// <summary>Par profissional × procedimento a consultar. Ordenado por (cpf, código) para o
    /// cursor de retomada ser determinístico.</summary>

    // ============================================================ agenda (config)

    public async Task<VarreduraAgendaDto> ObterAgendaAsync(CancellationToken ct)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);
        var agenda = await db.SisregVarreduraAgendas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UnidadeId == unidade.Id, ct);

        return await MontarAgendaDtoAsync(unidade, agenda, ct);
    }

    public async Task<VarreduraAgendaDto> SalvarAgendaAsync(SalvarVarreduraAgendaRequest request, CancellationToken ct)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);

        if (request.DiasAFrente < 1 || request.DiasAFrente > VarreduraSisregOpcoes.MaxDiasAFrente)
        {
            throw new ValidacaoException(
                "varredura.dias_invalidos",
                $"A janela tem que ficar entre 1 e {VarreduraSisregOpcoes.MaxDiasAFrente} dias — "
                + "o SISREG recusa consulta com intervalo maior que 31 dias.");
        }

        // A hora do disparo diário tem que ser um horário em que dá para INICIAR — o SISREG bloqueia
        // a exportação da agenda das 08:00 às 15:00, então uma varredura marcada para dentro dessa
        // faixa falharia todo dia.
        if (request.Ativo && !_opcoes.PodeIniciarNaHora(request.HoraLocal))
        {
            throw new ValidacaoException(
                "varredura.hora_fora_da_janela",
                $"A varredura não pode ser marcada entre {_opcoes.CorteEntradaLocal:HH\\:mm} e "
                + $"{_opcoes.BloqueioFimLocal:HH\\:mm} (Brasília): o SISREG bloqueia a exportação da "
                + $"agenda das {_opcoes.BloqueioInicioLocal:HH\\:mm} às {_opcoes.BloqueioFimLocal:HH\\:mm}. "
                + "Escolha um horário fora desse intervalo.");
        }

        var agenda = await db.SisregVarreduraAgendas.FirstOrDefaultAsync(x => x.UnidadeId == unidade.Id, ct);
        if (agenda is null)
        {
            agenda = new SisregVarreduraAgenda { UnidadeId = unidade.Id };
            db.SisregVarreduraAgendas.Add(agenda);
        }

        agenda.Ativo = request.Ativo;
        agenda.HoraLocal = request.HoraLocal;
        agenda.DiasAFrente = request.DiasAFrente;


        // PATCH, não PUT: quem manda payload mínimo não apaga em silêncio a decisão de não avisar
        // o paciente. Mesmo cuidado que a agenda do PEP toma com a janela noturna.
        if (request.EnviarConfirmacao is { } enviar) agenda.EnviarConfirmacao = enviar;

        agenda.AtualizadoEm = DateTime.UtcNow;

        // Ligar recalcula o próximo disparo; desligar zera. ProximoRunEm null significa NÃO
        // elegível (ver a entidade) — se ficasse null com a agenda ativa, o próximo boot dispararia
        // a varredura no meio do expediente.
        agenda.ProximoRunEm = request.Ativo
            ? DecididorVarreduraSisreg.ProximoDiario(request.HoraLocal, DateTime.UtcNow, Brasilia)
            : null;
        agenda.FalhasConsecutivas = 0;

        await db.SaveChangesAsync(ct);

        return await MontarAgendaDtoAsync(unidade, agenda, ct);
    }

    // ============================================================ disparo

    public async Task<VarreduraAceitaDto> IniciarAsync(CancellationToken ct)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);

        GarantirJanelaDeEntrada();
        GarantirSemTrabalhoVivo();

        var (execucaoId, mensagem) = await CriarExecucaoAsync(
            unidade, DisparoSincronizacao.Manual, usuarioAtual.UsuarioId, null, null, ct);

        return new VarreduraAceitaDto(execucaoId, mensagem);
    }

    public async Task<VarreduraAceitaDto> IniciarPeriodoAsync(
        IniciarVarreduraPeriodoRequest request, CancellationToken ct)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);

        if (request.DataInicio > request.DataFim)
        {
            throw new ValidacaoException(
                "varredura.periodo_invalido", "A data inicial não pode ser depois da data final.");
        }

        // O SISREG recusa exportação com intervalo maior que 31 dias por par. O split de 700
        // registros parte a janela por VOLUME, não por tamanho — então um período largo estouraria
        // já na primeira requisição. Para períodos maiores, rode em partes.
        var dias = request.DataFim.DayNumber - request.DataInicio.DayNumber;
        if (dias > VarreduraSisregOpcoes.MaxDiasAFrente)
        {
            throw new ValidacaoException(
                "varredura.periodo_longo",
                $"O período não pode passar de {VarreduraSisregOpcoes.MaxDiasAFrente} dias — o SISREG "
                + "recusa exportação com intervalo maior. Rode em partes para cobrir um período maior.");
        }

        GarantirJanelaDeEntrada();
        GarantirSemTrabalhoVivo();

        var (execucaoId, mensagem) = await CriarExecucaoAsync(
            unidade, DisparoSincronizacao.Manual, usuarioAtual.UsuarioId,
            request.DataInicio, request.DataFim, ct);

        return new VarreduraAceitaDto(execucaoId, mensagem);
    }

    /// <summary>Recusa iniciar dentro do bloqueio do <c>expo_solicitacoes</c> — o disparo falharia.</summary>
    private void GarantirJanelaDeEntrada()
    {
        var horaAgora = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Brasilia));
        if (!_opcoes.PodeIniciarNaHora(horaAgora))
        {
            throw new ValidacaoException(
                "varredura.hora_fora_da_janela",
                $"Não dá para iniciar uma varredura entre {_opcoes.CorteEntradaLocal:HH\\:mm} e "
                + $"{_opcoes.BloqueioFimLocal:HH\\:mm} (Brasília): o SISREG bloqueia a exportação da "
                + $"agenda das {_opcoes.BloqueioInicioLocal:HH\\:mm} às {_opcoes.BloqueioFimLocal:HH\\:mm}. "
                + "Tente fora desse intervalo.");
        }
    }

    private void GarantirSemTrabalhoVivo()
    {
        if (estadoVivo.ObterAtual() is not null)
        {
            throw new ConflitoException(
                "varredura.em_andamento",
                "Já há uma varredura em andamento. Espere ela terminar.");
        }

        if (importacaoEstadoVivo.ObterAtual() is not null)
        {
            throw new ConflitoException(
                "varredura.importacao_em_andamento",
                "Há uma importação de arquivo em andamento. Como as duas falam com o SISREG pela "
                + "mesma saída, espere a importação terminar.");
        }

        if (loteEstadoVivo.EmExecucao)
        {
            throw new ConflitoException(
                "varredura.lote_em_andamento",
                "Há uma sincronização de mapeamento de todas as unidades em andamento. Como as duas "
                + "falam com o SISREG pela mesma saída, espere ela terminar.");
        }
    }

    public async Task<Guid?> IniciarAgendadoAsync(Guid unidadeId, CancellationToken ct)
    {
        if (estadoVivo.ObterAtual() is not null
            || importacaoEstadoVivo.ObterAtual() is not null
            || loteEstadoVivo.EmExecucao)
        {
            return null;
        }

        var unidade = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(u => u.Id == unidadeId, ct);
        if (unidade is null) return null;

        try
        {
            // CriadoPor null: não existe usuário-robô. A autoria é o enum Disparo.
            var (execucaoId, _) = await CriarExecucaoAsync(
                unidade, DisparoSincronizacao.Agendado, null, null, null, ct);
            return execucaoId;
        }
        catch (ConflitoException)
        {
            // Corrida com um disparo manual: não é erro, o scheduler só reprograma.
            return null;
        }
    }

    public async Task<VarreduraAgendaDto> AlternarHistoricoAsync(
        AlternarHistoricoRequest request, CancellationToken ct)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);

        var agenda = await db.SisregVarreduraAgendas.FirstOrDefaultAsync(a => a.UnidadeId == unidade.Id, ct);
        if (agenda is null)
        {
            // A linha pode não existir: a unidade nunca configurou varredura diária. O histórico não
            // depende disso — cria a linha desligada para a diária e ligada só para o passado.
            agenda = new SisregVarreduraAgenda { UnidadeId = unidade.Id, Ativo = false };
            db.SisregVarreduraAgendas.Add(agenda);
        }

        agenda.HistoricoAtivo = request.Ativo;

        if (request.Reiniciar)
        {
            agenda.HistoricoCobertoDe = null;
            agenda.HistoricoFatiasVazias = 0;
            agenda.HistoricoConcluidoEm = null;
        }
        else if (request.Ativo)
        {
            // Religar uma unidade dada por concluída sem reiniciar não faria nada — o motor pula
            // quem tem HistoricoConcluidoEm. Limpar só a conclusão retoma de onde parou.
            agenda.HistoricoConcluidoEm = null;
            agenda.HistoricoFatiasVazias = 0;
        }

        agenda.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await ObterAgendaAsync(ct);
    }

    public async Task<Guid?> IniciarPeriodoAgendadoAsync(
        Guid unidadeId, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        if (estadoVivo.ObterAtual() is not null
            || importacaoEstadoVivo.ObterAtual() is not null
            || loteEstadoVivo.EmExecucao)
        {
            return null;
        }

        // O bloqueio 08:00–15:00 do expo_solicitacoes vale igual aqui: disparar dentro dele
        // gastaria a requisição para receber o alerta de aplicativo bloqueado.
        var horaLocal = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Brasilia));
        if (!_opcoes.PodeIniciarNaHora(horaLocal)) return null;

        var unidade = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(u => u.Id == unidadeId, ct);
        if (unidade is null) return null;

        try
        {
            var (execucaoId, _) = await CriarExecucaoAsync(
                unidade, DisparoSincronizacao.Agendado, null, inicio, fim, ct);
            return execucaoId;
        }
        catch (ConflitoException)
        {
            // Corrida com um disparo manual: não é erro, o motor só tenta de novo.
            return null;
        }
    }

    /// <summary>
    /// Até que dia a corrida diária desta unidade vai: o <b>último dia de escala ativa</b> dela.
    ///
    /// <para><b>Por que não é mais um número escolhido a dedo.</b> A janela era fixa em 21 dias
    /// porque cabia numa requisição. Mas a oferta vai muito além: medido em 05/09/2026, a mediana
    /// das unidades tem escala até 117 dias à frente, e o Centro de Radiologia até 2029. Comparar
    /// oferta que vai a dezembro com ocupação que para no dia 21 produz <b>disponibilidade
    /// fantasma</b> — toda vaga além da janela aparece livre por falta de dado, não por estar livre.
    /// Quem gerencia agenda em cima disso marca em cima de horário já ocupado.</para>
    ///
    /// <para>É também o que torna o cancelamento detectável: só faz sentido concluir "sumiu do
    /// SISREG" depois de olhar <b>todo</b> o futuro em que a solicitação poderia estar.</para>
    ///
    /// <para><b>Piso, não teto:</b> unidade sem escala nenhuma cadastrada continua varrendo
    /// <see cref="PisoDiasAFrente"/> dias. Derivar de um conjunto vazio daria "hoje", e a unidade
    /// pararia de importar em silêncio — trocaria um problema de alcance por um de cegueira total.
    /// É constante, e não configuração: escolher a janela era justamente o que produzia a
    /// disponibilidade fantasma, então devolver a escolha ao operador reabriria o problema.</para>
    /// </summary>
    private async Task<DateOnly> UltimoDiaDeAgendaAsync(
        Guid unidadeId, DateOnly hoje, SisregVarreduraAgenda? agenda, CancellationToken ct)
    {
        var piso = hoje.AddDays(PisoDiasAFrente);

        var ultimaEscala = await db.SisregEscalas
            .Where(e => e.UnidadeId == unidadeId
                && e.Status == StatusEscalaSisreg.Ativa
                && !e.Ausente
                && e.VigenciaFim >= hoje)
            .MaxAsync(e => (DateOnly?)e.VigenciaFim, ct);

        return ultimaEscala is { } fim && fim > piso ? fim : piso;
    }

    private async Task<(Guid ExecucaoId, string Mensagem)> CriarExecucaoAsync(
        Unidade unidade, DisparoSincronizacao disparo, Guid? usuarioId,
        DateOnly? janelaInicio, DateOnly? janelaFim, CancellationToken ct)
    {
        var agenda = await db.SisregVarreduraAgendas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UnidadeId == unidade.Id, ct);

        var hoje = HojeBrasilia();

        // Período explícito = backfill manual ou fatia do histórico; ausência = a corrida diária.
        var ehPeriodo = janelaInicio is not null;
        var inicio = janelaInicio ?? hoje;
        var fim = janelaFim ?? await UltimoDiaDeAgendaAsync(unidade.Id, hoje, agenda, ct);

        // Execuções de um processo que morreu (ou que uma exceção deixou pelo caminho) ficariam
        // "Rodando" para sempre na tela, e o operador não tem como saber que já acabou. Só chega
        // aqui quem passou pelo GarantirSemTrabalhoVivo, então não há risco de matar uma viva.
        await FecharOrfasAsync(unidade.Id, ct);

        var execucao = new SisregVarreduraExecucao
        {
            Id = Guid.CreateVersion7(),
            UnidadeId = unidade.Id,
            UnidadeNome = unidade.Nome,
            Disparo = disparo,
            Status = StatusVarredura.Pendente,
            JanelaInicio = inicio,
            JanelaFim = fim,
            // Sempre 1: a agenda vem inteira numa requisição. A coluna sobrevive pelo histórico —
            // execuções antigas registraram a cobertura real do modo por par (256/256 no CDT) e
            // apagá-la destruiria esse rastro.
            CombinacoesTotal = 1,
            IniciadoEm = DateTime.UtcNow,
            CriadoPor = usuarioId,
            CriadoPorNome = usuarioId is null ? null : await NomeDoUsuarioAsync(usuarioId.Value, ct),
        };

        db.SisregVarreduraExecucoes.Add(execucao);
        await db.SaveChangesAsync(ct);

        if (!fila.TentarEnfileirar(new VarreduraJob(execucao.Id, unidade.Id, disparo, usuarioId, ehPeriodo)))
        {
            execucao.Status = StatusVarredura.Erro;
            execucao.MensagemErro = "A fila de varredura já estava ocupada.";
            execucao.FinalizadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            throw new ConflitoException(
                "varredura.fila_cheia", "Já há uma varredura na fila. Tente de novo em instantes.");
        }

        return (execucao.Id,
            "Varredura enfileirada: agenda da unidade inteira em UMA requisição, de "
            + $"{execucao.JanelaInicio:dd/MM/yyyy} a {execucao.JanelaFim:dd/MM/yyyy}.");
    }

    // ============================================================ execução

    public async Task ExecutarAsync(VarreduraJob job, CancellationToken ct)
    {
        var execucao = await db.SisregVarreduraExecucoes.FirstOrDefaultAsync(x => x.Id == job.ExecucaoId, ct);
        if (execucao is null) return;

        var unidade = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(u => u.Id == job.UnidadeId, ct);
        var agenda = await db.SisregVarreduraAgendas.FirstOrDefaultAsync(x => x.UnidadeId == job.UnidadeId, ct);

        if (unidade is null)
        {
            await FinalizarAsync(execucao, StatusVarredura.Erro, "Unidade não encontrada.", ct);
            return;
        }

        // O serviço roda no runner: sem isto, ResolverExecutanteAsync não acha a unidade executante
        // e TODA marcação falharia por "unidade não resolvida". SuprimirConfirmacao vem do job: nas
        // varreduras por período (backfill), nenhuma marcação avisa o paciente por WhatsApp.
        importacao.DefinirContextoDeBackground(job.UsuarioId, unidade.Id, job.SuprimirConfirmacao);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var progresso = new ProgressoVarredura
        {
            ExecucaoId = execucao.Id,
            UnidadeId = unidade.Id,
            UnidadeNome = unidade.Nome,
            CombinacoesTotal = execucao.CombinacoesTotal,
        };
        estadoVivo.Iniciar(progresso, cts);

        execucao.Status = StatusVarredura.EmExecucao;
        await db.SaveChangesAsync(ct);

        try
        {
            await VarrerAsync(execucao, unidade, agenda, progresso, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            await FinalizarAsync(execucao, StatusVarredura.Cancelada, "Cancelada pelo operador.", ct, progresso);
        }
        catch (Exception ex) when (SisregWebSessao.EhCaptcha(ex))
        {
            // O CAPTCHA não é falha do nosso lado nem coisa que retentar resolva: relogar não
            // limpa. Para como PARCIAL, guardando onde parou — tudo que já entrou está gravado,
            // porque a importação acontece a cada combinação, não no fim.
            await TratarCaptchaAsync(execucao, agenda, progresso, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha na varredura da unidade {Unidade}.", unidade.Nome);
            await FinalizarAsync(execucao, StatusVarredura.Erro, ex.Message, ct, progresso);

            if (agenda is not null)
            {
                agenda.FalhasConsecutivas++;

                // Só no disparo agendado: um erro no botão manual não pode empurrar o horário
                // diário da unidade para longe.
                if (job.Disparo == DisparoSincronizacao.Agendado)
                    agenda.ProximoRunEm = DecididorVarreduraSisreg.ProximoAposErro(agenda, DateTime.UtcNow, Brasilia);
            }

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            estadoVivo.Finalizar();

            // NADA aqui pode lançar. Uma exceção no `finally` substitui a que estava subindo e
            // passa POR CIMA de todo o tratamento acima — foi assim que um DbUpdateConcurrency
            // deste SaveChanges escapou até o runner em 28/08/2026, deixando a execução eternamente
            // "Rodando" na tela e escondendo a causa real. Carimbar a última execução na agenda é
            // conveniência de tela; nunca vale derrubar o desfecho da varredura.
            try
            {
                if (agenda is not null)
                {
                    agenda.UltimaExecucaoEm = DateTime.UtcNow;
                    agenda.UltimaExecucaoId = execucao.Id;
                    agenda.AtualizadoEm = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Falha ao carimbar a última execução na agenda da unidade {Unidade}. A varredura "
                    + "em si já foi finalizada; só o rótulo da agenda ficou para trás.", unidade.Nome);
            }
        }
    }

    private async Task VarrerAsync(
        SisregVarreduraExecucao execucao,
        Unidade unidade,
        SisregVarreduraAgenda? agenda,
        ProgressoVarredura progresso,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(unidade.Cnes))
        {
            await FinalizarAsync(execucao, StatusVarredura.Erro,
                $"A unidade '{unidade.Nome}' não tem CNES cadastrado.", ct, progresso);
            return;
        }

        // O CNES da unidade é o filtro da exportação (campo "unidade" abaixo) — é ele que
        // delimita a agenda varrida, e é por isso que não existe mais double-check de sessão
        // aqui: a credencial em uso enxerga todas as unidades, então o CNES que a sessão reporta
        // é sempre o mesmo e barraria todas as unidades menos uma.
        var cnes = SoDigitos(unidade.Cnes!);

        // A agenda da unidade inteira em UMA requisição é o único caminho (ver
        // SisregVarreduraAgenda): além de custar 1 acesso em vez de um por par profissional ×
        // procedimento, ela devolve o mapeamento de graça — cada linha diz quem executa o quê.
        await VarrerUnidadeInteiraAsync(execucao, unidade, agenda, cnes, progresso, ct);
    }

    /// <summary>
    /// A agenda da unidade INTEIRA em uma requisição (<c>cpf=0</c>, <c>procedimento=0</c>).
    ///
    /// <para><b>Por que não há cursor de retomada aqui.</b> No modo por par, parar no meio custava
    /// caro: retomar do zero refaria centenas de requisições, então valia guardar em qual par
    /// parou. Aqui a leitura inteira custa uma requisição — refazer sai mais barato que a
    /// contabilidade de onde parou. O que já entrou continua gravado (a importação salva marcação a
    /// marcação); a rodada seguinte relê tudo e a idempotência por nº do SISREG resolve o resto.</para>
    ///
    /// <para>O rastreio por profissional × procedimento continua existindo, só que reconstituído
    /// <b>do arquivo</b>: cada linha traz o executante (colunas 4/5) e o seu próprio <c>pa</c>
    /// (coluna 1). É o mesmo detalhe de antes, obtido sem pagar uma requisição por par.</para>
    /// </summary>
    private async Task VarrerUnidadeInteiraAsync(
        SisregVarreduraExecucao execucao,
        Unidade unidade,
        // Null quando a unidade ainda não tem agenda configurada: a varredura manual pode ser
        // disparada antes disso, e exigir configuração para ver o resultado seria pedir fé.
        SisregVarreduraAgenda? agenda,
        string cnes,
        ProgressoVarredura progresso,
        CancellationToken ct)
    {
        progresso.ProfissionalAtual = "(unidade inteira)";
        progresso.ProcedimentoAtual = "(todos os procedimentos)";

        // A janela inteira em FATIAS de no máximo 31 dias: o SISREG recusa exportação com intervalo
        // maior, e a janela agora vai até a última escala da unidade (pode ser anos). O
        // `ExportarComTetoAsync` continua partindo cada fatia por VOLUME quando bate no teto
        // silencioso de 700 registros — as duas divisões se compõem, uma por tamanho e outra por
        // quantidade.
        var fatias = FatiarJanela(execucao.JanelaInicio, execucao.JanelaFim);
        var marcacoes = new List<MarcacaoSisreg>();

        for (var i = 0; i < fatias.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (fatiaInicio, fatiaFim) = fatias[i];

            // A tela precisa saber que está andando: uma corrida de 36 fatias sem sinal nenhum é
            // indistinguível de um motor travado.
            progresso.ProcedimentoAtual =
                $"lendo {fatiaInicio:dd/MM/yyyy} a {fatiaFim:dd/MM/yyyy} (fatia {i + 1} de {fatias.Count})";
            if (i > 0) await SalvarProgressoAsync(execucao, progresso, ct);

            marcacoes.AddRange(await ExportarComTetoAsync(
                cnes, fatiaInicio, fatiaFim,
                SemFiltro, SemFiltro, unidade.Nome, progresso, ct));
        }

        // Dedupe por nº do SISREG: o split por teto pode repetir uma linha na fronteira das metades.
        var novas = marcacoes
            .GroupBy(m => m.CodigoSolicitacao, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        Interlocked.Add(ref progresso.RegistrosEncontrados, novas.Count);

        // Grava ASSIM QUE LÊ, antes de qualquer processamento. A requisição é uma só, mas importar
        // milhares de linhas leva minutos — sem isto a tela fica em "0 lidas / 0 importadas" o
        // tempo todo e o operador não tem como saber se o motor está trabalhando ou travado. É a
        // diferença entre uma barra parada e "3.286 lidas, importando…".
        progresso.ProcedimentoAtual = $"{novas.Count} agendamentos lidos — atualizando o mapeamento…";
        await SalvarProgressoAsync(execucao, progresso, ct);

        // O mapeamento sai DE GRAÇA daqui, antes de importar: cada linha já diz quem executa e o
        // quê. Atualizar por AJAX custava 1 + N requisições (100 no CDT); aqui custa zero.
        var mapa = await AtualizarMapeamentoObservadoAsync(unidade.Id, novas, ct);

        // "1 requisição" não conta história nenhuma na tela. O que o operador precisa ver é o que
        // veio dentro dela: quantos profissionais e procedimentos a agenda tem, e o que é novidade.
        progresso.ProfissionalAtual =
            $"{mapa.Profissionais} profissionais · {mapa.Procedimentos} procedimentos"
            + (mapa.ProfissionaisNovos + mapa.ProcedimentosNovos > 0
                ? $" ({mapa.ProfissionaisNovos} prof. e {mapa.ProcedimentosNovos} proc. novos no mapeamento)"
                : string.Empty);

        // Os médicos descobertos aqui sobem para o hub FHIR como Practitioner, na mesma passagem.
        // Antes isso dependia de alguém lembrar de clicar em "Sincronizar profissionais (FHIR)" na
        // tela da unidade — e um médico só no SISREG, fora do hub, é identidade clínica que não
        // existe para o resto do sistema. Fala com o hub, não com o SISREG: não gasta orçamento
        // anti-robô. Falhar aqui NÃO derruba a varredura: a agenda já foi lida e vai ser importada
        // de qualquer forma; o vínculo FHIR se resolve na próxima passagem.
        try
        {
            var fhir = await mapeamento.SincronizarFhirNoContextoAsync(unidade, execucao.CriadoPor, ct);
            if (fhir.Criados + fhir.Vinculados > 0)
            {
                logger.LogInformation(
                    "SISREG_VARREDURA_FHIR: {Unidade} — {Criados} practitioners criados, {Vinc} vinculados.",
                    unidade.Nome, fhir.Criados, fhir.Vinculados);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "SISREG_VARREDURA_FHIR: falha ao sincronizar os profissionais de {Unidade} com o hub.",
                unidade.Nome);
        }

        // PRÉ-CARGA dos cadastros, em sessões paralelas do SER, antes de importar. A importação em
        // si tem de continuar serial (cria paciente, solicitação e exame no mesmo DbContext), mas
        // a espera pela rede não precisa ser: resolvida antes, ela encontra tudo pronto. Só roda
        // quando a fonte é o SER e a configuração pede mais de uma sessão; caso contrário devolve
        // na hora e nada muda.
        progresso.ProcedimentoAtual = "resolvendo cadastros no SER…";
        await SalvarProgressoAsync(execucao, progresso, ct);

        // O callback só mexe no progresso VIVO (memória), que é de onde o endpoint de status lê —
        // nada de escrever no banco a cada cadastro. É o que tira a tela do silêncio: esta fase
        // pode levar minutos sem criar uma linha sequer, e sem contador ela parece travada.
        var pre = await preCarga.ExecutarAsync(
            [.. novas.Select(m => m.CnsPaciente).Where(c => !string.IsNullOrWhiteSpace(c))!],
            ct,
            (feitos, total) =>
                progresso.ProcedimentoAtual = $"resolvendo cadastros no SER: {feitos} de {total}");

        if (pre.Sessoes > 1 && pre.Pedidos > 0)
        {
            logger.LogInformation(
                "SISREG_VARREDURA_PRECARGA: {Resolvidos}/{Pedidos} cadastros resolvidos em {Seg}s "
                + "com {Sessoes} sessões do SER.",
                pre.Resolvidos, pre.Pedidos, pre.DuracaoSegundos, pre.Sessoes);
        }

        // Grava JÁ. Sem isto a tela só mostraria o resultado do mapeamento junto com o primeiro
        // lote importado — e como cada paciente novo leva uns 3 segundos no SER, o operador ficaria
        // minutos olhando "atualizando o mapeamento…" sem saber se anda.
        progresso.ProcedimentoAtual = $"importando 0 de {novas.Count} agendamentos";
        await SalvarProgressoAsync(execucao, progresso, ct);

        // Em LOTES, não de uma vez: a requisição é uma só, mas a importação de milhares de linhas
        // leva minutos. Sem isto a tela ficaria congelada em "0 importadas" até o fim, e uma queda
        // no meio não teria deixado o progresso gravado — que é o mesmo motivo pelo qual o modo por
        // par importa a cada combinação em vez de acumular.
        for (var i = 0; i < novas.Count; i += LoteDeImportacao)
        {
            ct.ThrowIfCancellationRequested();

            var fatia = novas.Skip(i).Take(LoteDeImportacao).ToList();
            var resultado = await importacao.ImportarMarcacoesAsync(execucao.Id, fatia, ct);

            Interlocked.Add(ref progresso.Validos, resultado.Validos);
            Interlocked.Add(ref progresso.Invalidos, resultado.Invalidos);
            execucao.JaExistiam += resultado.JaExistiam;

            // A cobertura em combinações não diz nada aqui (é sempre 0/1 ou 1/1): quem informa o
            // andamento é a contagem de agendamentos processados.
            progresso.ProcedimentoAtual =
                $"importando {Math.Min(i + LoteDeImportacao, novas.Count)} de {novas.Count} agendamentos";
            await SalvarProgressoAsync(execucao, progresso, ct);
        }

        // O detalhe por par, reconstituído do próprio arquivo. Os contadores de válido/inválido são
        // da importação inteira, então não se pode reparti-los por grupo sem inventar número: o
        // item guarda o que É dele — quantos registros daquele par vieram. Requisições ficam em 0
        // em todos menos no primeiro, porque a requisição foi UMA para todos.
        var primeiro = true;
        foreach (var grupo in novas
            .GroupBy(m => (
                Cpf: m.CpfProfissionalExecutante ?? string.Empty,
                Codigo: SoDigitos(m.CodigoProcedimentoSisreg ?? string.Empty)))
            .OrderByDescending(g => g.Count()))
        {
            db.SisregVarreduraExecucaoItens.Add(new SisregVarreduraExecucaoItem
            {
                Id = Guid.CreateVersion7(),
                ExecucaoId = execucao.Id,
                ProfissionalCpf = grupo.Key.Cpf,
                ProfissionalNome = grupo.First().NomeProfissionalExecutante ?? "(não informado)",
                ProcedimentoCodigo = grupo.Key.Codigo,
                ProcedimentoNome = grupo.First().ProcedimentoTexto ?? "(não informado)",
                Requisicoes = primeiro ? progresso.Requisicoes : 0,
                RegistrosEncontrados = grupo.Count(),
                Validos = 0,
                Invalidos = 0,
                JaExistiam = 0,
                Observacao = primeiro
                    ? $"Agenda da unidade inteira em {progresso.Requisicoes} requisição(ões)."
                    : null,
            });
            primeiro = false;
        }

        // A corrida cobriu a janela inteira sem exceção — só aqui a ausência de um agendamento no
        // arquivo passa a significar alguma coisa. Se qualquer fatia tivesse falhado, este ponto não
        // teria sido alcançado (a exceção sobe e a execução vira Erro/Parcial).
        await DetectarAusentesAsync(execucao, unidade, novas, ct);

        progresso.CombinacoesFeitas = 1;
        if (agenda is not null)
        {
            agenda.PausadoAte = null;
            agenda.FalhasConsecutivas = 0;
        }

        await FinalizarAsync(execucao, StatusVarredura.Concluida, null, ct, progresso);

        logger.LogInformation(
            "SISREG_VARREDURA_OK (unidade inteira): {Unidade} — {Req} requisições, {Registros} "
            + "agendamentos, {Validos} importados, {Invalidos} pendências.",
            unidade.Nome, progresso.Requisicoes, progresso.RegistrosEncontrados,
            progresso.Validos, progresso.Invalidos);
    }

    /// <summary>
    /// Atualiza o mapeamento (profissionais e seus procedimentos) a partir das linhas já lidas —
    /// <b>sem gastar uma única requisição</b>.
    ///
    /// <para><b>Por que dá:</b> cada linha do export nomeia o executante (colunas 4/5) e o
    /// procedimento (colunas 1/3). O par que o AJAX <c>PROCEDIMENTOS_POR_PROFISSIONAIS_E_UPS</c>
    /// entrega a 1 + N requisições — 100 no CDT — está inteiro no arquivo que já baixamos.</para>
    ///
    /// <para><b>O que este mapa É e o que NÃO É.</b> Ele é o mapa OBSERVADO: quem de fato tem
    /// agenda na janela varrida. O AJAX entrega o CATÁLOGO: o que o profissional PODE fazer, mesmo
    /// sem nenhum agendamento. São coisas diferentes, e por isso <b>nada aqui marca
    /// <c>Ausente</c></b>: não aparecer no arquivo significa "sem agenda nestes dias", não "saiu da
    /// unidade". Marcar ausente por omissão apagaria da tela, a cada varredura, todo profissional
    /// de férias — e a habilitação dele junto.</para>
    ///
    /// <para><b>Decisão do operador é intocável:</b> <c>Habilitado</c> e <c>EnviarConfirmacao</c>
    /// de linha existente nunca são alterados. Linha nova nasce desabilitada (o opt-in de sempre),
    /// mas HERDA o <c>EnviarConfirmacao</c> das irmãs do mesmo código na unidade — senão um
    /// profissional novo entraria mudo num procedimento cujo aviso já está ligado, e ninguém
    /// perceberia.</para>
    ///
    /// <para><b>Os pares "novos" são, em boa parte, os ITENS de dentro de um GRUPO já habilitado.</b>
    /// Conferido no CDT em 27/08/2026 contra o export real: o arquivo mostrou 33 profissionais e 82
    /// pares, dos quais 49 já estavam mapeados e 33 não — o mapeamento guarda o grupo
    /// (<c>1402000</c>) e a agenda devolve o item (<c>1402077</c>). Eles entram desabilitados, então
    /// não passam a custar requisição no modo por par; ganham nome e passam a existir na tela, que
    /// é o que faltava para o operador decidir sobre o aviso ao paciente item a item.</para>
    ///
    /// <para>Na mesma medição, 223 dos 272 pares mapeados NÃO tinham agenda na janela. É a prova
    /// concreta de por que a omissão não pode virar <c>Ausente</c>: apagaria 82% do mapeamento.</para>
    /// </summary>
    private async Task<MapaObservado> AtualizarMapeamentoObservadoAsync(
        Guid unidadeId, IReadOnlyList<MarcacaoSisreg> marcacoes, CancellationToken ct)
    {
        var observados = marcacoes
            .Where(m => !string.IsNullOrWhiteSpace(m.CpfProfissionalExecutante))
            .GroupBy(m => m.CpfProfissionalExecutante!)
            .ToList();
        if (observados.Count == 0) return new MapaObservado(0, 0, 0, 0);

        // CONTEXTO PRÓPRIO, não o `db` do escopo. O `db` está no meio da varredura, rastreando a
        // execução e a agenda; misturar nele um grafo de profissionais e procedimentos novos
        // envenenou o change tracker e a falha só apareceu no SaveChanges seguinte, como
        // DbUpdateConcurrencyException ("esperava afetar 1 linha, afetou 0") — longe da causa.
        // É o mesmo motivo pelo qual o lote de importação escreve o progresso por um contexto à
        // parte.
        await using var ctx = await dbFactory.CreateDbContextAsync(ct);

        var existentes = await ctx.SisregProfissionaisUnidade
            .Include(p => p.Procedimentos)
            .Where(p => p.UnidadeId == unidadeId)
            .ToListAsync(ct);

        var porCpf = existentes.ToDictionary(p => p.Cpf, StringComparer.Ordinal);

        // Quem já avisa, por código, na unidade inteira — a régua que a linha nova herda.
        var avisaPorCodigo = existentes
            .SelectMany(p => p.Procedimentos)
            .Where(x => x.EnviarConfirmacao)
            .Select(x => x.Codigo)
            .ToHashSet(StringComparer.Ordinal);

        var agora = DateTime.UtcNow;
        var profissionaisNovos = 0;
        var procedimentosNovos = 0;

        foreach (var grupo in observados)
        {
            if (!porCpf.TryGetValue(grupo.Key, out var profissional))
            {
                profissional = new SisregProfissionalUnidade
                {
                    Id = Guid.CreateVersion7(),
                    UnidadeId = unidadeId,
                    Cpf = grupo.Key,
                    Nome = grupo.First().NomeProfissionalExecutante ?? "(não informado)",
                    // Nasce LIGADO. Nascia desligado porque habilitar decidia o custo da varredura
                    // (uma requisição por par) — essa razão morreu com a agenda vindo inteira. Hoje
                    // "habilitado" decide quem sobe ao hub FHIR como Practitioner, e quem aparece
                    // na agenda importada está atendendo de verdade: é a prova mais forte que existe
                    // de que aquele médico pertence à unidade. Deixá-lo de fora do hub por um
                    // default herdado de outro problema seria perder identidade clínica de graça.
                    Habilitado = true,
                    VistoEm = agora,
                    CriadoEm = agora,
                };
                ctx.SisregProfissionaisUnidade.Add(profissional);
                porCpf[grupo.Key] = profissional;
                profissionaisNovos++;
            }
            else
            {
                if (grupo.First().NomeProfissionalExecutante is { Length: > 0 } nome) profissional.Nome = nome;
                profissional.VistoEm = agora;
                // Tem agenda AGORA — é a prova mais forte de presença que existe.
                profissional.Ausente = false;
                profissional.AtualizadoEm = agora;
            }

            foreach (var porCodigo in grupo
                .Select(m => (Codigo: SoDigitos(m.CodigoProcedimentoSisreg ?? string.Empty), m.ProcedimentoTexto))
                .Where(x => x.Codigo.Length > 0)
                .GroupBy(x => x.Codigo, StringComparer.Ordinal))
            {
                var atual = profissional.Procedimentos
                    .FirstOrDefault(x => string.Equals(x.Codigo, porCodigo.Key, StringComparison.Ordinal));

                if (atual is null)
                {
                    // Add explícito no DbSet, não na coleção de navegação: com a PK já preenchida,
                    // o fix-up da coleção podia classificar a linha nova como Modified, o INSERT
                    // nunca sair e o UPDATE seguinte não achar linha nenhuma.
                    ctx.Set<SisregProcedimentoProfissional>().Add(new SisregProcedimentoProfissional
                    {
                        Id = Guid.CreateVersion7(),
                        ProfissionalId = profissional.Id,
                        Codigo = porCodigo.Key,
                        Nome = porCodigo.First().ProcedimentoTexto ?? "(não informado)",
                        Habilitado = false,
                        EnviarConfirmacao = avisaPorCodigo.Contains(porCodigo.Key),
                        Grupo = porCodigo.Key.EndsWith("000", StringComparison.Ordinal),
                        VistoEm = agora,
                    });
                    procedimentosNovos++;
                }
                else
                {
                    if (porCodigo.First().ProcedimentoTexto is { Length: > 0 } nome) atual.Nome = nome;
                    atual.VistoEm = agora;
                    atual.Ausente = false;
                }
            }
        }

        await ctx.SaveChangesAsync(ct);

        var procedimentosDistintos = marcacoes
            .Select(m => SoDigitos(m.CodigoProcedimentoSisreg ?? string.Empty))
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Count();

        logger.LogInformation(
            "SISREG_MAPEAMENTO_OBSERVADO: unidade {Unidade} — {Profissionais} profissionais e "
            + "{Procedimentos} procedimentos vistos na agenda; {ProfNovos} profissionais e "
            + "{ProcNovos} procedimentos novos, sem custo de requisição.",
            unidadeId, observados.Count, procedimentosDistintos, profissionaisNovos, procedimentosNovos);

        return new MapaObservado(
            observados.Count, procedimentosDistintos, profissionaisNovos, procedimentosNovos);
    }

    /// <summary>O que a agenda revelou — vai para o status vivo, que é o que o operador lê.</summary>
    private sealed record MapaObservado(
        int Profissionais, int Procedimentos, int ProfissionaisNovos, int ProcedimentosNovos);

    /// <summary>
    /// Exporta a agenda de UM par profissional × procedimento e devolve as marcações.
    ///
    /// <para><b>Teto de 700 registros por exportação</b>, medido em 03/08/2026: intervalos de 61 e
    /// de 212 dias devolveram exatamente 700 — é limite do SISREG, não coincidência. E é
    /// truncamento <b>silencioso</b>: o cabeçalho informa 700 e as linhas são 700, sem nada
    /// dizendo que faltou. Por isso, ao bater no teto, a janela é partida ao meio e reconsultada:
    /// perder agendamento sem avisar seria o pior desfecho possível aqui.</para>
    /// </summary>
    /// <summary>
    /// Uma exportação, partindo a janela ao meio quando o SISREG trunca.
    ///
    /// <para><c>cpf</c> e <c>procedimento</c> aceitam <see cref="SemFiltro"/> — é assim que a
    /// agenda da unidade inteira sai de uma vez.</para>
    /// </summary>
    /// <summary>
    /// Parte a janela em pedaços que o SISREG aceita (máximo <see cref="VarreduraSisregOpcoes.MaxDiasAFrente"/>
    /// + 1 dias por exportação). As fatias <b>encostam sem sobrepor</b>: cada uma começa no dia
    /// seguinte ao fim da anterior — um dia de folga viraria um dia de agenda invisível por mês.
    /// </summary>
    internal static List<(DateOnly Inicio, DateOnly Fim)> FatiarJanela(DateOnly inicio, DateOnly fim)
    {
        var fatias = new List<(DateOnly, DateOnly)>();
        if (fim < inicio) return fatias;

        // O limite do SISREG é sobre a DIFERENÇA entre as datas, então uma fatia de N dias de
        // diferença cobre N+1 dias de calendário.
        var passo = VarreduraSisregOpcoes.MaxDiasAFrente;
        var cursor = inicio;

        while (cursor <= fim)
        {
            var fatiaFim = cursor.AddDays(passo);
            if (fatiaFim > fim) fatiaFim = fim;
            fatias.Add((cursor, fatiaFim));
            cursor = fatiaFim.AddDays(1);
        }

        return fatias;
    }

    /// <summary>
    /// Quem estava marcado nesta janela e NÃO veio no arquivo: candidato a cancelamento no SISREG.
    ///
    /// <para><b>Por que só aqui, no fim de uma corrida completa.</b> Enquanto a janela era de 21
    /// dias, ausência não queria dizer nada — um agendamento remarcado para dali a dois meses sumia
    /// da janela e pareceria cancelado. Agora que a corrida cobre até a última escala da unidade, o
    /// remarcado aparece em alguma fatia; sobra a ausência de verdade.</para>
    ///
    /// <para><b>Não cancela nada.</b> Registra na fila de alterações para um humano confirmar.
    /// Cancelar atendimento automaticamente a partir de raspagem é o tipo de erro que se paga com
    /// paciente sem consulta.</para>
    ///
    /// <para><b>Freio de sanidade:</b> se a proporção de ausentes for alta demais, não é
    /// cancelamento em massa — é leitura incompleta (exportação truncada, sessão trocada no meio).
    /// Nesse caso não registra nada e deixa o aviso no log. Concluir aqui seria despejar centenas de
    /// falsos cancelamentos na fila e destruir a confiança dela.</para>
    /// </summary>
    private async Task DetectarAusentesAsync(
        SisregVarreduraExecucao execucao, Unidade unidade,
        List<MarcacaoSisreg> lidas, CancellationToken ct)
    {
        var codigosLidos = lidas
            .Select(m => m.CodigoSolicitacao)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.Ordinal);

        // A janela é de datas de Brasília; `data_agendada` é instante UTC. Converter pelo
        // utilitário central (e não com `.Date` cru) é o que impede a janela de escorregar três
        // horas e varrer o dia errado — o mesmo deslize que faria a agenda mostrar número diferente
        // depois das 21h.
        var inicioUtc = FusoBrasilia.DeBrasiliaParaUtc(execucao.JanelaInicio.ToDateTime(TimeOnly.MinValue));
        var fimUtc = FusoBrasilia.DeBrasiliaParaUtc(execucao.JanelaFim.AddDays(1).ToDateTime(TimeOnly.MinValue));

        // Só o que o SISREG conhece: solicitação manual sem nº (extra-SUS, sentinela "0000") nunca
        // vem no arquivo, e chamá-la de ausente seria acusar o que nasceu fora dele.
        var marcadas = await db.Solicitacoes
            .Where(s => s.UnidadeExecutanteId == unidade.Id
                && s.ExcluidoEm == null
                && s.CanceladoEm == null
                && s.DataAgendada >= inicioUtc
                && s.DataAgendada < fimUtc
                && s.CodigoSolicitacao != null
                && s.CodigoSolicitacao != "0000"
                && s.RawSisreg != null)
            .Select(s => new { s.Id, s.CodigoSolicitacao, s.UnidadeExecutanteId, s.UnidadeSolicitanteId })
            .ToListAsync(ct);

        if (marcadas.Count == 0) return;

        var ausentes = marcadas.Where(s => !codigosLidos.Contains(s.CodigoSolicitacao!)).ToList();
        if (ausentes.Count == 0) return;

        var proporcao = (double)ausentes.Count / marcadas.Count;
        if (proporcao > LimiteAusentesSuspeito)
        {
            logger.LogWarning(
                "SISREG_AUSENTES_SUSPEITO: {Unidade} — {Ausentes} de {Total} agendamentos não vieram "
                + "no arquivo ({Pct:P0}). Alto demais para ser cancelamento: tratado como leitura "
                + "incompleta e IGNORADO.",
                unidade.Nome, ausentes.Count, marcadas.Count, proporcao);
            return;
        }

        // Não duplica: alteração de ausência ainda pendente para a mesma solicitação já está na fila.
        var ids = ausentes.Select(a => a.Id).ToList();
        var jaNaFila = await db.SisregAlteracoesAgenda
            .Where(a => ids.Contains(a.SolicitacaoId)
                && a.Tipo == TipoAlteracaoAgenda.Ausente
                && a.TratadaEm == null)
            .Select(a => a.SolicitacaoId)
            .ToListAsync(ct);

        var agora = DateTime.UtcNow;
        var novos = 0;
        foreach (var ausente in ausentes.Where(a => !jaNaFila.Contains(a.Id)))
        {
            db.SisregAlteracoesAgenda.Add(new SisregAlteracaoAgenda
            {
                Id = Guid.CreateVersion7(),
                SolicitacaoId = ausente.Id,
                CodigoSolicitacao = ausente.CodigoSolicitacao,
                Tipo = TipoAlteracaoAgenda.Ausente,
                ValorAntes = "agendado",
                ValorDepois = "não veio no arquivo do SISREG",
                UnidadeExecutanteId = ausente.UnidadeExecutanteId,
                UnidadeSolicitanteId = ausente.UnidadeSolicitanteId,
                DetectadaEm = agora,
            });
            novos++;
        }

        if (novos == 0) return;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "SISREG_AUSENTES: {Unidade} — {Novos} agendamento(s) sumiram do SISREG e entraram na fila "
            + "de alterações para confirmação.", unidade.Nome, novos);
    }

    private async Task<List<MarcacaoSisreg>> ExportarComTetoAsync(
        string cnes,
        DateOnly inicio,
        DateOnly fim,
        string cpf,
        string procedimento,
        string rotulo,
        ProgressoVarredura progresso,
        CancellationToken ct)
    {
        var texto = await ExportarAsync(cnes, inicio, fim, cpf, procedimento, ct);
        Interlocked.Increment(ref progresso.Requisicoes);

        var parsed = AgendaTxtParser.Parse(texto, NomeArquivoSintetico(procedimento, inicio, fim));

        // O TOTAL do cabeçalho, não a contagem de linhas PARSEADAS: no corte silencioso o cabeçalho
        // também diz 700, e uma única linha recusada pelo parser faria 700 virar 699 aqui — o corte
        // passaria despercebido e o agendamento sumiria sem aviso. Medido em 27/08/2026: o total
        // declarado bateu exatamente com as linhas nos quatro recortes testados, inclusive num de
        // 3.286 (o teto de 700 não se aplica ao recorte amplo, mas a guarda continua valendo).
        var lidos = parsed.Cabecalho.Total ?? parsed.Marcacoes.Count + parsed.Rejeitadas.Count;

        var bateuNoTeto = lidos >= TetoRegistrosPorExportacao;
        if (!bateuNoTeto || inicio >= fim) return [.. parsed.Marcacoes];

        // Parte ao meio e reconsulta cada metade. A recursão termina porque a janela encolhe a cada
        // nível e para quando inicio == fim (um único dia).
        var meio = inicio.AddDays((fim.DayNumber - inicio.DayNumber) / 2);
        logger.LogWarning(
            "SISREG_EXPORT_TRUNCADA: {Proc} em {Ini}..{Fim} declarou {Lidos} registros (teto {Teto}) — "
            + "partindo a janela em {Ini}..{Meio} e {Meio2}..{Fim}.",
            rotulo, inicio, fim, lidos, TetoRegistrosPorExportacao, inicio, meio, meio.AddDays(1), fim);

        var esquerda = await ExportarComTetoAsync(cnes, inicio, meio, cpf, procedimento, rotulo, progresso, ct);
        var direita = await ExportarComTetoAsync(cnes, meio.AddDays(1), fim, cpf, procedimento, rotulo, progresso, ct);

        esquerda.AddRange(direita);
        return esquerda;
    }

    private async Task<string> ExportarAsync(
        string cnes, DateOnly inicio, DateOnly fim, string cpf, string procedimento, CancellationToken ct)
    {
        // Pausa ANTES da requisição. Fica aqui e não na sessão HTTP de propósito: atrasar a sessão
        // penalizaria as telas interativas (mapeamento, CADSUS), que não têm nada a ver com o
        // volume da varredura.
        if (_opcoes.PausaMs > 0) await Task.Delay(_opcoes.PausaMs, ct);

        return await sessao.PostFormAsync(Caminho, new Dictionary<string, string>
        {
            // Cultura invariante explícita: em "dd/MM/yyyy" a barra é o SEPARADOR DA CULTURA, não
            // um literal. Num host com locale que use "." ou "-", a data sairia deformada e o
            // SISREG responderia vazio — falha silenciosa, idêntica a uma agenda sem movimento.
            ["data1"] = inicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["data2"] = fim.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["cpf"] = cpf,
            ["procedimento"] = procedimento,
            ["tp_arquivo"] = "0", // 0 = TXT, 1 = CSV
            ["etapa"] = "exportar",
            ["unidade"] = cnes,
        }, ct);
    }

    /// <summary>
    /// O parser do TXT usa o nome do arquivo para derivar a unidade executante no formato CSV.
    /// Aqui o cabeçalho já traz o CNES, mas um nome estável ajuda a proveniência da pendência.
    /// </summary>
    private static string NomeArquivoSintetico(string procedimento, DateOnly inicio, DateOnly fim) =>
        $"sisreg-{(procedimento == SemFiltro ? "unidade" : procedimento)}-{inicio:yyyyMMdd}-{fim:yyyyMMdd}.txt";

    private async Task TratarCaptchaAsync(
        SisregVarreduraExecucao execucao,
        SisregVarreduraAgenda? agenda,
        ProgressoVarredura progresso,
        CancellationToken ct)
    {
        var pausaAte = DateTime.UtcNow.AddHours(_opcoes.CaptchaPausaHoras);

        if (agenda is not null) agenda.PausadoAte = pausaAte;

        await FinalizarAsync(execucao, StatusVarredura.Parcial,
            "O SISREG passou a exigir CAPTCHA (proteção anti-robô por volume de acessos) e a varredura "
            + "parou no meio. O que já entrou está salvo. Relogar não resolve: alguém precisa abrir o "
            + "SISREG no navegador com o operador desta unidade e resolver o CAPTCHA. A varredura "
            + "retoma de onde parou.", ct, progresso);

        logger.LogError(
            "SISREG_VARREDURA_CAPTCHA: unidade {Unidade} parou em {Prof}/{Proc} — {Feitas}/{Total} "
            + "combinações, {Req} requisições, {Validos} importados. Pausada até {Pausa}.",
            execucao.UnidadeNome, progresso.ProfissionalAtual, progresso.ProcedimentoAtual,
            progresso.CombinacoesFeitas, progresso.CombinacoesTotal, progresso.Requisicoes,
            progresso.Validos, pausaAte);
    }

    private async Task FinalizarAsync(
        SisregVarreduraExecucao execucao,
        StatusVarredura status,
        string? mensagem,
        CancellationToken ct,
        ProgressoVarredura? progresso = null)
    {
        execucao.Status = status;
        execucao.MensagemErro = mensagem is null ? null : Truncar(mensagem, 2000);
        execucao.FinalizadoEm = DateTime.UtcNow;
        execucao.DuracaoSegundos = (int)(execucao.FinalizadoEm.Value - execucao.IniciadoEm).TotalSeconds;

        if (progresso is not null) CopiarProgresso(execucao, progresso);

        await db.SaveChangesAsync(ct);
    }

    private async Task SalvarProgressoAsync(
        SisregVarreduraExecucao execucao, ProgressoVarredura progresso, CancellationToken ct)
    {
        CopiarProgresso(execucao, progresso);
        await db.SaveChangesAsync(ct);
    }

    private static void CopiarProgresso(SisregVarreduraExecucao execucao, ProgressoVarredura progresso)
    {
        execucao.CombinacoesFeitas = progresso.CombinacoesFeitas;
        execucao.Requisicoes = progresso.Requisicoes;
        execucao.RegistrosEncontrados = progresso.RegistrosEncontrados;
        execucao.Validos = progresso.Validos;
        execucao.Invalidos = progresso.Invalidos;
    }

    // ============================================================ consultas

    public Task<StatusVarreduraVivo?> ObterStatusAsync(CancellationToken ct) =>
        Task.FromResult(estadoVivo.ObterAtual());

    public bool Cancelar() => estadoVivo.Cancelar();

    public async Task<IReadOnlyList<VarreduraExecucaoDto>> ListarExecucoesAsync(int limite, CancellationToken ct)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);

        return await db.SisregVarreduraExecucoes.AsNoTracking()
            .Where(x => x.UnidadeId == unidade.Id)
            .OrderByDescending(x => x.IniciadoEm)
            .Take(Math.Clamp(limite, 1, 100))
            .Select(x => new VarreduraExecucaoDto(
                x.Id, x.UnidadeId, x.UnidadeNome, x.Disparo, x.Status, x.JanelaInicio, x.JanelaFim,
                x.CombinacoesTotal, x.CombinacoesFeitas, x.Requisicoes, x.RegistrosEncontrados,
                x.Validos, x.Invalidos, x.JaExistiam, x.MensagemErro, x.IniciadoEm, x.FinalizadoEm,
                x.DuracaoSegundos, x.CriadoPorNome))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VarreduraExecucaoItemDto>> ListarItensAsync(
        Guid execucaoId, CancellationToken ct)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);

        // A execução tem que ser DESTA unidade — senão o header X-Unidade-Id de uma unidade abriria
        // o detalhe da varredura de outra.
        var pertence = await db.SisregVarreduraExecucoes.AsNoTracking()
            .AnyAsync(x => x.Id == execucaoId && x.UnidadeId == unidade.Id, ct);
        if (!pertence)
        {
            throw new NaoEncontradoException(
                "varredura.execucao_nao_encontrada", "Execução de varredura não encontrada nesta unidade.");
        }

        return await db.SisregVarreduraExecucaoItens.AsNoTracking()
            .Where(i => i.ExecucaoId == execucaoId)
            .OrderBy(i => i.ProfissionalNome)
            .ThenBy(i => i.ProcedimentoNome)
            .Select(i => new VarreduraExecucaoItemDto(
                i.Id, i.ProfissionalNome, i.ProcedimentoCodigo, i.ProcedimentoNome,
                i.Requisicoes, i.RegistrosEncontrados, i.Validos, i.Invalidos, i.JaExistiam, i.Observacao))
            .ToListAsync(ct);
    }

    // ============================================================ apoio

    /// <summary>
    /// Fecha execuções que ficaram <c>Pendente</c>/<c>EmExecucao</c> sem ninguém tocando nelas —
    /// restart no meio da rodada, ou exceção que escapou do tratamento. Sem isto a lista de
    /// varreduras recentes mostra "Rodando" indefinidamente e o operador fica esperando algo que
    /// já morreu.
    /// </summary>
    private async Task FecharOrfasAsync(Guid unidadeId, CancellationToken ct)
    {
        var orfas = await db.SisregVarreduraExecucoes
            .Where(e => e.UnidadeId == unidadeId
                        && (e.Status == StatusVarredura.Pendente || e.Status == StatusVarredura.EmExecucao))
            .ToListAsync(ct);
        if (orfas.Count == 0) return;

        foreach (var o in orfas)
        {
            o.Status = StatusVarredura.Erro;
            o.MensagemErro ??= "Varredura interrompida (o serviço reiniciou ou a execução falhou "
                               + "sem registrar o motivo). Nada do que já entrou foi perdido.";
            o.FinalizadoEm ??= DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        logger.LogWarning(
            "SISREG_VARREDURA_ORFA: {Qtd} execução(ões) da unidade {Unidade} estavam presas em "
            + "andamento e foram fechadas como erro.", orfas.Count, unidadeId);
    }

    private async Task<VarreduraAgendaDto> MontarAgendaDtoAsync(
        Unidade unidade, SisregVarreduraAgenda? agenda, CancellationToken ct)
    {
        return new VarreduraAgendaDto(
            unidade.Id,
            unidade.Nome,
            agenda?.Ativo ?? false,
            agenda?.HoraLocal ?? new TimeOnly(4, 30),
            agenda?.DiasAFrente ?? 21,
            agenda?.ProximoRunEm,
            agenda?.PausadoAte,
            agenda?.UltimaExecucaoEm,
            agenda?.FalhasConsecutivas ?? 0,
            // UMA requisição para toda a agenda da unidade, sempre.
            1,
            _opcoes.TetoPorExecucao,
            _opcoes.BloqueioInicioLocal,
            _opcoes.BloqueioFimLocal,
            _opcoes.CorteEntradaLocal,
            // Unidade sem linha de configuração NÃO envia: o gatilho é opt-in.
            agenda?.EnviarConfirmacao ?? false,
            agenda?.HistoricoAtivo ?? false,
            agenda?.HistoricoCobertoDe,
            agenda?.HistoricoConcluidoEm);
    }

    private async Task<string?> NomeDoUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
        await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.NomeCompleto)
            .FirstOrDefaultAsync(ct);

    private static DateOnly HojeBrasilia() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Brasilia));

    private static string SoDigitos(string valor) => new([.. valor.Where(char.IsDigit)]);

    private static string Truncar(string valor, int max) =>
        valor.Length <= max ? valor : valor[..max];
}

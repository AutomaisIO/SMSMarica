using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Notificacoes.AgendamentosExternos;

/// <summary>O que uma passada da varredura fez — vai para o log.</summary>
public sealed record ResultadoVarreduraAgendamentosExternos(
    int Lidos, int Novos, int Reagendados, int Desmarcados, int AguardandoHora, int AvisosDeCancelamento = 0);

public interface IAgendamentoExternoService
{
    /// <summary>
    /// Lê os agendamentos que o SER, o SERNIT e o ESUS SG mostram hoje (os espelhos) e põe na fila
    /// o aviso de cada agendamento NOVO; remarcação vira aviso novo e o que saiu de "Agendada"
    /// deixa de sair. Idempotente — roda de tempos em tempos. Salva.
    /// </summary>
    Task<ResultadoVarreduraAgendamentosExternos> SincronizarAsync(CancellationToken ct = default);
}

/// <summary>
/// A ponte entre os espelhos do SER/SERNIT/ESUS SG e o aviso ao paciente (10/10/2026). Até então
/// o agendamento desses sistemas não avisava ninguém: só o SISREG tinha gatilho.
///
/// <para><b>Por que varredura, e não o gatilho da sincronização</b> (<c>ser_gatilho</c> & cia.): o
/// <c>ProcessadoEm</c> daquelas filas quer dizer "visto por uma pessoa" na tela de Notificações, e
/// o espelho já é a verdade do que está agendado. Ler o espelho inteiro é barato (milhares de linhas,
/// não milhões) e se corrige sozinho: o que uma passada perder, a seguinte acha.</para>
///
/// <para><b>Só avisa de amanhã em diante.</b> No próprio dia, para um atendimento fora do
/// município, a mensagem não ajuda — e o que vence enquanto espera a janela de envio cai na guarda
/// de "data futura" do envio.</para>
/// </summary>
public sealed class AgendamentoExternoService(
    SmsMaisDbContext db,
    IComunicacaoPacienteService comunicacoes,
    Confirmacoes.IConfirmacaoConfiguracaoService regras,
    IAvisoRegulacaoConfiguracaoService avisosRegulacao,
    ILogger<AgendamentoExternoService> logger) : IAgendamentoExternoService
{
    /// <summary>
    /// O aviso de cancelamento espera um pouco antes de poder sair. Uma leitura do espelho pode
    /// pegar o pedido no meio da rodada (o ESUS lê a fila antes dos agendados) ou a regulação pode
    /// devolver e marcar de novo o mesmo dia: a passada seguinte (10 min) acha o agendamento de volta
    /// e o aviso é dispensado antes de sair. Dizer "foi cancelado" a quem segue marcado é o pior erro.
    /// </summary>
    internal static readonly TimeSpan CarenciaDoCancelamento = TimeSpan.FromMinutes(30);

    /// <summary>
    /// O SER dá a hora só no evento "Agendar" do histórico, que é lido depois da grade (às vezes horas
    /// depois, na mesma madrugada). Espera-se por ele até aqui; passado isso, o aviso sai só com o dia
    /// — melhor que não avisar.
    /// </summary>
    internal static readonly TimeSpan EsperaPelaHoraDoSer = TimeSpan.FromHours(24);

    /// <summary>Estados em que a comunicação ainda não chegou ao paciente com os dados — é o que a
    /// remarcação e o desmarque encerram.</summary>
    private static readonly StatusComunicacao[] AindaNaoSaiu =
    [
        StatusComunicacao.Pendente,
        StatusComunicacao.AguardandoVerificacaoCadastral,
        StatusComunicacao.AguardandoTelefoneVerificado,
        StatusComunicacao.AguardandoCorrecaoContato,
    ];

    /// <summary>Um agendamento como o espelho o mostra agora.</summary>
    internal sealed record Lido(
        SistemaRegulacao Sistema, Guid OrigemId, string Numero, Guid PacienteId, TipoAgendamento Tipo,
        string Procedimento, DateOnly Data, TimeOnly? Hora, string? Local, bool EsperandoHora);

    public async Task<ResultadoVarreduraAgendamentosExternos> SincronizarAsync(CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var avisar = (await regras.ObterAsync(ct)).AvisoAgendamentoExternoHabilitado;

        var lidos = new List<Lido>();
        lidos.AddRange(await LerSerAsync(hoje, agora, ct));
        lidos.AddRange(await LerSernitAsync(hoje, ct));
        lidos.AddRange(await LerEsusSgAsync(hoje, ct));

        var vigentes = await db.AgendamentosExternos
            .Where(a => a.Situacao == SituacaoAgendamentoExterno.Ativo)
            .ToListAsync(ct);
        var vigentePorOrigem = vigentes.ToDictionary(a => (a.Sistema, a.OrigemId));

        int novos = 0, reagendados = 0, desmarcados = 0, aguardandoHora = 0;
        var vistos = new HashSet<(SistemaRegulacao, Guid)>();

        foreach (var l in lidos)
        {
            vistos.Add((l.Sistema, l.OrigemId));
            vigentePorOrigem.TryGetValue((l.Sistema, l.OrigemId), out var atual);

            if (atual is not null && atual.Data == l.Data && atual.PacienteId == l.PacienteId)
            {
                AtualizarDetalhes(atual, l, agora);
                continue;
            }

            // Mudou o dia (ou o pedido mudou de dono — correção de identidade): o aviso anterior não
            // vale mais. Fica de histórico, e o que ainda não tinha saído não sai.
            if (atual is not null)
            {
                await EncerrarAsync(atual, SituacaoAgendamentoExterno.Reagendado,
                    atual.PacienteId == l.PacienteId
                        ? $"Remarcado no {Rotulo(l.Sistema)} para {l.Data:dd/MM/yyyy}: sai um aviso novo."
                        : $"O pedido no {Rotulo(l.Sistema)} passou para outro paciente.",
                    agora, ct);
                reagendados++;
            }

            // Só se avisa de amanhã em diante — e com a chave ligada (desligada, nada novo nasce;
            // ao religar, os agendamentos futuros são achados na passada seguinte).
            if (l.Data <= hoje) continue;

            // Voltou para "Agendada" com o MESMO dia de um desmarque recente: o aviso de cancelamento
            // que ainda espera a carência não sai mais. Antes da chave e da espera pela hora do SER
            // — as duas pulam a reativação, e o aviso sairia sobre um atendimento que voltou a valer.
            await DispensarCancelamentoQueNaoValeMaisAsync(l, agora, ct);

            if (!avisar) continue;
            if (l.EsperandoHora) { aguardandoHora++; continue; }

            var agendamento = await ReativarOuCriarAsync(l, agora, ct);
            await comunicacoes.EnfileirarExternoAsync(agendamento, ct);
            novos++;
        }

        // Saiu de "Agendada" no sistema de destino (cancelado, voltou à fila, saiu da fila).
        // O que já passou fica como está: é o histórico do que foi avisado. Só se desmarca o que
        // ainda ia acontecer.
        var saidos = vigentes.Where(a => !vistos.Contains((a.Sistema, a.OrigemId)) && a.Data >= hoje).ToList();
        var motivos = await MotivosDoDesmarqueAsync(saidos, ct);
        var regrasExternas = saidos.Count == 0 ? null : await avisosRegulacao.RegrasExternasAsync(ct);
        var avisosCancelamento = 0;
        foreach (var a in saidos)
        {
            var motivo = motivos.GetValueOrDefault(a.Id, MotivoDesmarqueExterno.Outro);
            await EncerrarAsync(a, SituacaoAgendamentoExterno.Desmarcado, TextoDoDesmarque(motivo, a.Sistema), agora, ct);
            a.MotivoDesmarque = motivo;
            desmarcados++;

            // Aviso de cancelamento (21/10/2026): só quando o sistema DIZ que o atendimento caiu
            // (cancelado ou devolvido à fila), com a chave da regulação ligada — e ligar vale
            // daqui para frente.
            if (motivo is MotivoDesmarqueExterno.Cancelado or MotivoDesmarqueExterno.VoltouParaFila
                && regrasExternas!.TryGetValue(a.Sistema, out var r)
                && r.AvisoCancelamentoHabilitado
                && r.AvisoCancelamentoLigadoEm is { } ligadoEm && ligadoEm <= agora)
            {
                await comunicacoes.EnfileirarExternoAsync(
                    a, FinalidadeComunicacao.CancelamentoAgendamento, agora + CarenciaDoCancelamento, ct);
                avisosCancelamento++;
            }
        }

        await db.SaveChangesAsync(ct);

        var resultado = new ResultadoVarreduraAgendamentosExternos(
            lidos.Count, novos, reagendados, desmarcados, aguardandoHora, avisosCancelamento);
        if (novos + reagendados + desmarcados > 0)
            logger.LogInformation(
                "Agendamentos do SER/SERNIT/ESUS SG: {Lidos} lidos, {Novos} avisos novos na fila, {Reagendados} "
                + "remarcados, {Desmarcados} desmarcados ({Cancelamentos} com aviso de cancelamento), "
                + "{AguardandoHora} esperando a hora do SER.",
                resultado.Lidos, novos, reagendados, desmarcados, avisosCancelamento, aguardandoHora);
        return resultado;
    }

    /// <summary>
    /// Por que cada agendamento saiu de "Agendada", pelo que o espelho mostra AGORA. Só "Cancelada"
    /// (SER/SERNIT) e "Em fila" (os três) dizem que o atendimento caiu. Pendência fica de fora: o SER
    /// às vezes pendencia e reagenda o mesmo dia. A saída da fila do ESUS também: o ESUS não diz por
    /// quê (ADR-0063 — "saiu da fila" não é cancelado).
    /// </summary>
    private async Task<Dictionary<Guid, MotivoDesmarqueExterno>> MotivosDoDesmarqueAsync(
        List<AgendamentoExterno> saidos, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, MotivoDesmarqueExterno>();
        if (saidos.Count == 0) return resultado;

        List<Guid> Origens(SistemaRegulacao s) => saidos.Where(a => a.Sistema == s).Select(a => a.OrigemId).ToList();

        var ser = Origens(SistemaRegulacao.Ser);
        var situacoesSer = ser.Count == 0 ? [] : await db.SerSolicitacoes.AsNoTracking()
            .Where(s => ser.Contains(s.Id) && s.ExcluidoEm == null)
            .Select(s => new { s.Id, s.Situacao })
            .ToDictionaryAsync(s => s.Id, s => s.Situacao switch
            {
                SituacaoSer.Cancelada => MotivoDesmarqueExterno.Cancelado,
                SituacaoSer.EmFila => MotivoDesmarqueExterno.VoltouParaFila,
                _ => MotivoDesmarqueExterno.Outro,
            }, ct);

        var sernit = Origens(SistemaRegulacao.Sernit);
        var situacoesSernit = sernit.Count == 0 ? [] : await db.SernitSolicitacoes.AsNoTracking()
            .Where(s => sernit.Contains(s.Id) && s.ExcluidoEm == null)
            .Select(s => new { s.Id, s.Situacao })
            .ToDictionaryAsync(s => s.Id, s => s.Situacao switch
            {
                SituacaoSernit.Cancelada => MotivoDesmarqueExterno.Cancelado,
                SituacaoSernit.EmFila => MotivoDesmarqueExterno.VoltouParaFila,
                _ => MotivoDesmarqueExterno.Outro,
            }, ct);

        var esus = Origens(SistemaRegulacao.EsusSg);
        var situacoesEsus = esus.Count == 0 ? [] : await db.EsusSgSolicitacoes.AsNoTracking()
            .Where(s => esus.Contains(s.Id) && s.ExcluidoEm == null)
            .Select(s => new { s.Id, s.Situacao })
            .ToDictionaryAsync(s => s.Id, s => s.Situacao == SituacaoEsusSg.EmFila
                ? MotivoDesmarqueExterno.VoltouParaFila
                : MotivoDesmarqueExterno.Outro, ct);

        foreach (var a in saidos)
        {
            var doSistema = a.Sistema switch
            {
                SistemaRegulacao.Ser => situacoesSer,
                SistemaRegulacao.Sernit => situacoesSernit,
                _ => situacoesEsus,
            };
            resultado[a.Id] = doSistema.GetValueOrDefault(a.OrigemId, MotivoDesmarqueExterno.Outro);
        }
        return resultado;
    }

    private static string TextoDoDesmarque(MotivoDesmarqueExterno motivo, SistemaRegulacao sistema) => motivo switch
    {
        MotivoDesmarqueExterno.Cancelado => $"O pedido foi cancelado no {Rotulo(sistema)}.",
        MotivoDesmarqueExterno.VoltouParaFila => $"O pedido voltou para a fila no {Rotulo(sistema)}.",
        _ => $"O pedido saiu de \"Agendada\" no {Rotulo(sistema)}.",
    };

    /// <summary>Hora, local e procedimento podem chegar (ou mudar) depois — o dia é o que define o
    /// agendamento; o resto se corrige em silêncio, antes de o paciente pedir os dados.</summary>
    private static void AtualizarDetalhes(AgendamentoExterno a, Lido l, DateTime agora)
    {
        var mudou = false;
        if (l.Hora is not null && a.Hora != l.Hora) { a.Hora = l.Hora; mudou = true; }
        if (!string.IsNullOrWhiteSpace(l.Local) && a.LocalNome != l.Local) { a.LocalNome = l.Local; mudou = true; }
        if (a.Procedimento != l.Procedimento && l.Procedimento.Length > 0) { a.Procedimento = l.Procedimento; mudou = true; }
        if (a.Tipo != l.Tipo) { a.Tipo = l.Tipo; mudou = true; }
        if (mudou) a.AtualizadoEm = agora;
    }

    /// <summary>
    /// O mesmo pedido pode sair de "Agendada" e voltar com o MESMO dia (o SER às vezes pendencia e
    /// reagenda igual). Reativar a linha antiga evita avisar duas vezes do mesmo atendimento — e, se
    /// o aviso dela não tinha chegado a sair, ele volta para a fila.
    /// </summary>
    private async Task<AgendamentoExterno> ReativarOuCriarAsync(Lido l, DateTime agora, CancellationToken ct)
    {
        // Quem já recebeu "foi cancelado" deste agendamento não é reativado em silêncio: nasce linha
        // nova, e o aviso do agendamento sai de novo — senão a pessoa ficaria com o cancelamento
        // como última palavra sobre um atendimento que voltou a valer.
        var anterior = await db.AgendamentosExternos
            .Where(a => a.Sistema == l.Sistema && a.OrigemId == l.OrigemId && a.Data == l.Data
                        && a.PacienteId == l.PacienteId && a.Situacao != SituacaoAgendamentoExterno.Ativo
                        && !db.ComunicacoesPaciente.Any(c => c.AgendamentoExternoId == a.Id
                            && c.Finalidade == FinalidadeComunicacao.CancelamentoAgendamento
                            && c.EnviadoEm != null))
            .OrderByDescending(a => a.CriadoEm)
            .FirstOrDefaultAsync(ct);
        if (anterior is not null)
        {
            anterior.Situacao = SituacaoAgendamentoExterno.Ativo;
            anterior.EncerradoEm = null;
            anterior.MotivoDesmarque = null;
            AtualizarDetalhes(anterior, l, agora);
            anterior.AtualizadoEm = agora;
            return anterior;
        }

        var novo = new AgendamentoExterno
        {
            Id = Guid.CreateVersion7(),
            Sistema = l.Sistema,
            OrigemId = l.OrigemId,
            NumeroExterno = l.Numero,
            PacienteId = l.PacienteId,
            Tipo = l.Tipo,
            Procedimento = l.Procedimento,
            Data = l.Data,
            Hora = l.Hora,
            LocalNome = l.Local,
            Situacao = SituacaoAgendamentoExterno.Ativo,
            CriadoEm = agora,
        };
        db.AgendamentosExternos.Add(novo);
        return novo;
    }

    private async Task DispensarCancelamentoQueNaoValeMaisAsync(Lido l, DateTime agora, CancellationToken ct)
    {
        var emEspera = await db.ComunicacoesPaciente
            .Where(c => c.Finalidade == FinalidadeComunicacao.CancelamentoAgendamento
                && AindaNaoSaiu.Contains(c.Status)
                && c.AgendamentoExterno != null
                && c.AgendamentoExterno.Sistema == l.Sistema
                && c.AgendamentoExterno.OrigemId == l.OrigemId
                && c.AgendamentoExterno.Data == l.Data)
            .ToListAsync(ct);
        foreach (var c in emEspera)
        {
            c.Status = StatusComunicacao.Dispensada;
            c.MotivoFalha = $"O agendamento voltou a valer no {Rotulo(l.Sistema)} antes de o aviso sair.";
            c.ProximaTentativaEm = null;
            c.AtualizadoEm = agora;
        }
    }

    private async Task EncerrarAsync(
        AgendamentoExterno a, SituacaoAgendamentoExterno situacao, string motivo, DateTime agora, CancellationToken ct)
    {
        a.Situacao = situacao;
        a.EncerradoEm = agora;
        a.AtualizadoEm = agora;

        var naFila = await db.ComunicacoesPaciente
            .Where(c => c.AgendamentoExternoId == a.Id && AindaNaoSaiu.Contains(c.Status))
            .ToListAsync(ct);
        foreach (var c in naFila)
        {
            c.Status = StatusComunicacao.Dispensada;
            c.MotivoFalha = motivo.Length <= 1000 ? motivo : motivo[..1000];
            c.ProximaTentativaEm = null;
            c.AtualizadoEm = agora;
        }
    }

    // ---------------- leitura dos espelhos ----------------

    private async Task<List<Lido>> LerSerAsync(DateOnly hoje, DateTime agora, CancellationToken ct)
    {
        var linhas = await db.SerSolicitacoes.AsNoTracking()
            .Where(s => s.Situacao == SituacaoSer.Agendada && s.ExcluidoEm == null && s.PacienteId != null)
            .Select(s => new
            {
                s.Id, s.IdSer, s.Tipo, s.Recurso, s.AgendadoParaTexto, s.UnidadeExecutora, s.PacienteId,
                s.SituacaoMudouEm, s.SincronizadoEm,
            })
            .ToListAsync(ct);

        var comData = linhas
            .Select(s => (s, LeituraAgendamentoExterno.DataHora(s.AgendadoParaTexto).Data))
            .Where(x => x.Data is { } d && d >= hoje)
            .ToList();

        // A hora mora no evento "Agendar" do histórico — o mais novo de cada pedido.
        var ids = comData.Select(x => x.s.Id).ToList();
        var eventos = ids.Count == 0
            ? []
            : await db.SerEventos.AsNoTracking()
                .Where(e => ids.Contains(e.SerSolicitacaoId) && e.Evento == "Agendar")
                .Select(e => new { e.SerSolicitacaoId, e.DataEvento, e.Observacao, e.UnidadeExecutora })
                .ToListAsync(ct);
        var ultimoAgendar = eventos
            .GroupBy(e => e.SerSolicitacaoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.DataEvento).First());

        return comData.Select(x =>
        {
            var (s, data) = x;
            ultimoAgendar.TryGetValue(s.Id, out var ev);
            var hora = LeituraAgendamentoExterno.HoraDoEventoAgendar(ev?.Observacao, data!.Value);
            var desde = s.SituacaoMudouEm ?? s.SincronizadoEm;
            return new Lido(
                SistemaRegulacao.Ser, s.Id, s.IdSer, s.PacienteId!.Value,
                s.Tipo == TipoRecursoSer.Exame ? TipoAgendamento.Exame : TipoAgendamento.Consulta,
                LeituraAgendamentoExterno.Espacos(s.Recurso),
                data.Value, hora,
                NuloSeVazio(LeituraAgendamentoExterno.Espacos(s.UnidadeExecutora ?? ev?.UnidadeExecutora)),
                EsperandoHora: hora is null && agora - desde < EsperaPelaHoraDoSer);
        }).ToList();
    }

    private async Task<List<Lido>> LerSernitAsync(DateOnly hoje, CancellationToken ct)
    {
        var linhas = await db.SernitSolicitacoes.AsNoTracking()
            .Where(s => s.Situacao == SituacaoSernit.Agendada && s.ExcluidoEm == null && s.PacienteId != null)
            .Select(s => new { s.Id, s.IdSernit, s.Tipo, s.Recurso, s.AgendadoParaTexto, s.UnidadeExecutora, s.PacienteId })
            .ToListAsync(ct);

        var lidos = new List<Lido>();
        foreach (var s in linhas)
        {
            var (data, hora, local) = LeituraAgendamentoExterno.DataHora(s.AgendadoParaTexto);
            if (data is not { } d || d < hoje) continue;
            lidos.Add(new Lido(
                SistemaRegulacao.Sernit, s.Id, s.IdSernit, s.PacienteId!.Value,
                s.Tipo == TipoRecursoSernit.Exame ? TipoAgendamento.Exame : TipoAgendamento.Consulta,
                LeituraAgendamentoExterno.Espacos(s.Recurso), d, hora,
                NuloSeVazio(local ?? LeituraAgendamentoExterno.Espacos(s.UnidadeExecutora)),
                EsperandoHora: false));
        }
        return lidos;
    }

    private async Task<List<Lido>> LerEsusSgAsync(DateOnly hoje, CancellationToken ct)
    {
        var linhas = await db.EsusSgSolicitacoes.AsNoTracking()
            .Where(s => s.Situacao == SituacaoEsusSg.Agendada && s.ExcluidoEm == null && s.PacienteId != null
                        && s.DataAgendada != null && s.DataAgendada >= hoje)
            .Select(s => new
            {
                s.Id, s.IdEsusSg, s.Tipo, s.Recurso, s.DataAgendada, s.DataHoraAgendadaTexto, s.UnidadeExecutora,
                s.Local, s.PacienteId,
            })
            .ToListAsync(ct);

        return linhas.Select(s =>
        {
            var (dataTexto, hora, _) = LeituraAgendamentoExterno.DataHora(s.DataHoraAgendadaTexto);
            return new Lido(
                SistemaRegulacao.EsusSg, s.Id, s.IdEsusSg, s.PacienteId!.Value,
                s.Tipo == TipoRecursoEsusSg.Exame ? TipoAgendamento.Exame : TipoAgendamento.Consulta,
                LeituraAgendamentoExterno.Espacos(s.Recurso), s.DataAgendada!.Value,
                // A hora do texto só vale se o texto fala do mesmo dia da coluna.
                dataTexto == s.DataAgendada ? hora : null,
                LeituraAgendamentoExterno.LocalEsusSg(s.UnidadeExecutora),
                EsperandoHora: false);
        }).ToList();
    }

    private static string? NuloSeVazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>"SER", "SERNIT", "ESUS" — como a equipe e o paciente chamam o sistema.</summary>
    public static string Rotulo(SistemaRegulacao s) => s switch
    {
        SistemaRegulacao.Ser => "SER",
        SistemaRegulacao.Sernit => "SERNIT",
        SistemaRegulacao.EsusSg => "ESUS",
        _ => s.ToString().ToUpperInvariant(),
    };
}

/// <summary>
/// Roda a varredura dos agendamentos do SER/SERNIT/ESUS SG de tempos em tempos. Enfileirar é barato
/// e pode rodar a qualquer hora; quem respeita a janela de envio é o enviador.
/// </summary>
public sealed class AgendamentoExternoWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AgendamentoExternoWorker> logger) : BackgroundService
{
    /// <summary>Os espelhos mudam em lotes de madrugada (e quando alguém sincroniza à mão): 10 min
    /// põe o aviso na fila logo depois de cada lote, sem custo que importe.</summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IAgendamentoExternoService>()
                    .SincronizarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Nunca deixa subir: BackgroundServiceExceptionBehavior=StopHost derrubaria a API.
                // Corrida com a resposta do paciente (xmin) cai aqui também — a próxima passada refaz.
                logger.LogError(ex, "Falha na varredura dos agendamentos do SER/SERNIT/ESUS SG — tenta de novo.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}

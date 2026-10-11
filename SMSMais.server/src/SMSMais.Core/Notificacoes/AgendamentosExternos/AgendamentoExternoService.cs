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
    int Lidos, int Novos, int Reagendados, int Desmarcados, int AguardandoHora);

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
    ILogger<AgendamentoExternoService> logger) : IAgendamentoExternoService
{
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
            if (l.Data <= hoje || !avisar) continue;
            if (l.EsperandoHora) { aguardandoHora++; continue; }

            var agendamento = await ReativarOuCriarAsync(l, agora, ct);
            await comunicacoes.EnfileirarExternoAsync(agendamento, ct);
            novos++;
        }

        // Saiu de "Agendada" no sistema de destino (cancelado, voltou à fila, saiu da fila).
        foreach (var a in vigentes.Where(a => !vistos.Contains((a.Sistema, a.OrigemId))))
        {
            // O que já passou fica como está: é o histórico do que foi avisado. Só se desmarca o que
            // ainda ia acontecer.
            if (a.Data < hoje) continue;
            await EncerrarAsync(a, SituacaoAgendamentoExterno.Desmarcado,
                $"O pedido saiu de \"Agendada\" no {Rotulo(a.Sistema)}.", agora, ct);
            desmarcados++;
        }

        await db.SaveChangesAsync(ct);

        var resultado = new ResultadoVarreduraAgendamentosExternos(lidos.Count, novos, reagendados, desmarcados, aguardandoHora);
        if (novos + reagendados + desmarcados > 0)
            logger.LogInformation(
                "Agendamentos do SER/SERNIT/ESUS SG: {Lidos} lidos, {Novos} avisos novos na fila, {Reagendados} "
                + "remarcados, {Desmarcados} desmarcados, {AguardandoHora} esperando a hora do SER.",
                resultado.Lidos, novos, reagendados, desmarcados, aguardandoHora);
        return resultado;
    }

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
        var anterior = await db.AgendamentosExternos
            .Where(a => a.Sistema == l.Sistema && a.OrigemId == l.OrigemId && a.Data == l.Data
                        && a.PacienteId == l.PacienteId && a.Situacao != SituacaoAgendamentoExterno.Ativo)
            .OrderByDescending(a => a.CriadoEm)
            .FirstOrDefaultAsync(ct);
        if (anterior is not null)
        {
            anterior.Situacao = SituacaoAgendamentoExterno.Ativo;
            anterior.EncerradoEm = null;
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

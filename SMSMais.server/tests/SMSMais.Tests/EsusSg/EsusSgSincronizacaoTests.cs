using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.EsusSg;
using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Core.Integracoes.EsusSgWeb.Varredura;
using SMSMais.Core.Regulacao.Conciliacao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.EsusSg;

/// <summary>
/// O motor do ESUS de São Gonçalo contra o banco real, com o ESUS de mentira (ADR-0063).
///
/// <para>O que prendem: o espelho nasce e se atualiza pela chave (tipo, fil_id); a trilha é
/// MONTADA dos marcos (inclusão, cada sessão agendada) sem duplicar numa segunda leitura; pedido
/// que sai da fila e aparece nos agendados MUDA de situação; pedido que some das duas listas vira
/// "saiu da fila" — mas só quando a leitura fechou a conta, nunca por falha de leitura.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class EsusSgSincronizacaoTests(PostgresFixture fixture)
{
    private static string Id() => Random.Shared.NextInt64(10_000_000, 99_999_999).ToString();

    private sealed class LeitorFake : IEsusSgLeitorService
    {
        public List<EsusSgLinhaFila> Fila { get; set; } = [];
        public List<EsusSgLinhaAgendado> Agendados { get; set; } = [];
        public bool FilaIncompleta { get; set; }

        public Task<LeituraEsusSg<EsusSgLinhaFila>> LerFilaAsync(TipoRecursoEsusSg tipo, CancellationToken ct)
        {
            var linhas = tipo == TipoRecursoEsusSg.Exame ? Fila : [];
            return Task.FromResult(new LeituraEsusSg<EsusSgLinhaFila>(
                linhas, FilaIncompleta ? linhas.Count + 1 : linhas.Count, linhas.Count, 1));
        }

        public Task<LeituraEsusSg<EsusSgLinhaAgendado>> LerAgendadosAsync(
            TipoRecursoEsusSg tipo, DateOnly de, DateOnly ate, CancellationToken ct)
        {
            var linhas = tipo == TipoRecursoEsusSg.Exame
                ? Agendados.Where(a => a.DataAgendada >= de && a.DataAgendada <= ate).ToList()
                : [];
            return Task.FromResult(new LeituraEsusSg<EsusSgLinhaAgendado>(linhas, linhas.Count, linhas.Count, 1));
        }

        public Task<IReadOnlyList<EsusSgRecursoCatalogo>> LerCatalogoExameAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EsusSgRecursoCatalogo>>([]);

        /// <summary>Histórico por pes_id e trilha por id de exame — o comparecimento.</summary>
        public Dictionary<string, List<EsusSgHistoricoExame>> Historico { get; } = [];
        public Dictionary<long, List<EsusSgEventoExame>> Trilhas { get; } = [];
        public List<string> HistoricosLidos { get; } = [];

        public Task<IReadOnlyList<EsusSgHistoricoExame>> LerHistoricoExamesAsync(
            string pessoaIdEsus, DateOnly ate, CancellationToken ct)
        {
            HistoricosLidos.Add(pessoaIdEsus);
            return Task.FromResult<IReadOnlyList<EsusSgHistoricoExame>>(
                Historico.TryGetValue(pessoaIdEsus, out var h) ? h : []);
        }

        public Task<IReadOnlyList<EsusSgEventoExame>> LerTrilhaExameAsync(long idExame, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EsusSgEventoExame>>(Trilhas.TryGetValue(idExame, out var t) ? t : []);
    }

    private sealed class CatalogoFake : IEsusSgCatalogoSyncService
    {
        public Task<EsusSgCatalogoSyncResultado> SincronizarAsync(CancellationToken ct) =>
            Task.FromResult(new EsusSgCatalogoSyncResultado(0, 0, 0, 0));
    }

    private sealed class ConciliacaoFake : IRegulacaoConciliacaoService
    {
        public Task<int> ConciliarSerAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult(0);
        public Task<int> ConciliarSernitAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult(0);
        public Task<int> ConciliarSisregAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult(0);
        public Task<int> ConciliarEsusSgAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult(0);
    }

    private static EsusSgSincronizacaoService Motor(SmsMaisDbContext db, IEsusSgLeitorService leitor) =>
        new(db, leitor, new ConciliacaoFake(), new CatalogoFake(), NullLogger<EsusSgSincronizacaoService>.Instance);

    private static EsusSgLinhaFila NaFila(string id, string prioridade = "A REGULAR", string pendencia = "NAO") => new(
        id, "TRATAMENTO DE RETINA (PPI)", "3251", null, new DateOnly(2025, 1, 10), new DateOnly(2025, 1, 20),
        prioridade, null, pendencia, 5, 100, "DR SOLICITANTE", "MUNICÍPIO DE MARICÁ - 0000001", "OPERADORA MARICA",
        null, "p" + id, "PACIENTE " + id, "52998224725", "700000000000005", new DateOnly(1960, 5, 1), "F",
        "MAE", "(21) 99999-0000", null, "MARICA", "CENTRO");

    private static EsusSgLinhaAgendado Agendado(string id, string eap, DateOnly dia, string hora = "08:00") => new(
        id, eap, "TRATAMENTO DE RETINA (PPI)", new DateOnly(2025, 1, 10), new DateOnly(2025, 1, 20), "A REGULAR",
        null, "DR SOLICITANTE", "MUNICÍPIO DE MARICÁ - 0000001", "OPERADORA MARICA", "p" + id, "PACIENTE " + id,
        "52998224725", "21999990000", "VISATTO UNIDADE RODOSHOPPING", null, "TRATAMENTO DE RETINA", "ÚNICO",
        dia, $"{dia:dd/MM/yyyy} {hora}:00", FusoBrasilia.DeBrasiliaParaUtc(dia.ToDateTime(TimeOnly.Parse(hora))),
        "OPERADOR SG", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), true, false, null, null, null);

    private static async Task<Guid> RodarAsync(SmsMaisDbContext db, IEsusSgLeitorService leitor)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        return await Motor(db, leitor).ExecutarAsync(
            ModoVarreduraEsusSg.Diaria, DisparoSincronizacao.Manual,
            hoje.AddDays(-45), hoje.AddDays(400), null, "teste", null, CancellationToken.None);
    }

    [Fact]
    public async Task Primeira_rodada_cria_espelho_trilha_e_gatilhos_e_a_segunda_nao_duplica()
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var (a, b) = (Id(), Id());
        var leitor = new LeitorFake
        {
            Fila = [NaFila(a)],
            // O mesmo pedido com DUAS sessões (medido: 97 agendamentos em 94 pedidos em mar/2019).
            Agendados = [Agendado(b, "s1", hoje.AddDays(10)), Agendado(b, "s2", hoje.AddDays(40))],
        };

        await using (var db = fixture.CriarDbContext())
        {
            var execId = await RodarAsync(db, leitor);
            var exec = await db.EsusSgVarreduraExecucoes.AsNoTracking().FirstAsync(x => x.Id == execId);
            Assert.Equal(StatusVarreduraEsusSg.Concluida, exec.Status);
        }

        await using (var db = fixture.CriarDbContext())
        {
            var sa = await db.EsusSgSolicitacoes.Include(x => x.Eventos).FirstAsync(x => x.IdEsusSg == a);
            Assert.Equal(SituacaoEsusSg.EmFila, sa.Situacao);
            Assert.Equal(5, sa.PosicaoFila);
            Assert.NotNull(sa.PacienteConciliarEm); // vai para a conciliação com o hub
            Assert.Contains(sa.Eventos, e => e.TipoEvento == TipoEventoExterno.Solicitar && e.Usuario == "OPERADORA MARICA");

            var sb = await db.EsusSgSolicitacoes.Include(x => x.Eventos).FirstAsync(x => x.IdEsusSg == b);
            Assert.Equal(SituacaoEsusSg.Agendada, sb.Situacao);
            Assert.Equal(hoje.AddDays(10), sb.DataAgendada); // a PRÓXIMA sessão representa o pedido
            Assert.Equal(2, sb.Eventos.Count(e => e.TipoEvento == TipoEventoExterno.Agendar));

            Assert.Equal(1, await db.EsusSgGatilhos.CountAsync(g => g.IdEsusSg == a && g.Tipo == TipoGatilhoEsusSg.NovaSolicitacao));
        }

        // Relê o mesmo: nada de evento nem gatilho novo.
        await using (var db = fixture.CriarDbContext())
        {
            await RodarAsync(db, leitor);
        }
        await using (var db = fixture.CriarDbContext())
        {
            Assert.Equal(1, await db.EsusSgEventos.CountAsync(e => e.EsusSgSolicitacao!.IdEsusSg == a));
            Assert.Equal(3, await db.EsusSgEventos.CountAsync(e => e.EsusSgSolicitacao!.IdEsusSg == b));
            Assert.Equal(1, await db.EsusSgGatilhos.CountAsync(g => g.IdEsusSg == a));
        }
    }

    private static EsusSgEventoExame Evento(string filId, EfetivacaoEsusSg? efetivacao, DateTime registro, int ordem,
        string? motivo = null) => new(filId, efetivacao is null ? "AGENDADO" : efetivacao.ToString(), efetivacao,
        efetivacao == EfetivacaoEsusSg.Efetivado ? registro : null, motivo, registro, null, ordem);

    /// <summary>
    /// Comparecimento: para o agendamento que passou, a varredura lê o histórico da pessoa e a trilha do
    /// exame do mesmo dia, e grava o que a unidade apontou. Exame do mesmo dia de OUTRO pedido (fil_id de
    /// outro) não conta; agendamento futuro não é lido; efetivado não é relido.
    /// </summary>
    [Fact]
    public async Task Comparecimento_vem_da_trilha_do_exame_do_mesmo_dia_e_efetivado_nao_e_relido()
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var dia = hoje.AddDays(-5);
        var (veio, faltou, aberto, outro, futuro) = (Id(), Id(), Id(), Id(), Id());
        var t0 = FusoBrasilia.DeBrasiliaParaUtc(dia.ToDateTime(new TimeOnly(15, 42)));
        var leitor = new LeitorFake
        {
            Agendados = [Agendado(veio, "a", dia), Agendado(faltou, "b", dia), Agendado(aberto, "c", dia),
                         Agendado(outro, "d", dia), Agendado(futuro, "e", hoje.AddDays(10))],
        };
        long n = Random.Shared.NextInt64(1_000_000, 9_000_000);
        void Hist(string id, long exame, DateOnly quando) =>
            leitor.Historico["p" + id] = [new EsusSgHistoricoExame("999", [exame], quando)];
        Hist(veio, n, dia);
        leitor.Trilhas[n] = [Evento(veio, null, t0.AddDays(-9), 1), Evento(veio, EfetivacaoEsusSg.Efetivado, t0, 3),
                             Evento(veio, EfetivacaoEsusSg.Efetivado, t0, 2)];
        Hist(faltou, n + 1, dia);
        leitor.Trilhas[n + 1] = [Evento(faltou, null, t0.AddDays(-9), 1),
                                 Evento(faltou, EfetivacaoEsusSg.NaoEfetivado, t0, 2, "Não Compareceu")];
        Hist(aberto, n + 2, dia);
        leitor.Trilhas[n + 2] = [Evento(aberto, null, t0.AddDays(-9), 1)];
        Hist(outro, n + 3, dia);
        leitor.Trilhas[n + 3] = [Evento("12345", EfetivacaoEsusSg.Efetivado, t0, 1)];
        Hist(futuro, n + 4, hoje.AddDays(10));

        await using (var db = fixture.CriarDbContext()) await RodarAsync(db, leitor);

        await using (var db = fixture.CriarDbContext())
        {
            var sVeio = await db.EsusSgSolicitacoes.Include(x => x.Eventos).FirstAsync(x => x.IdEsusSg == veio);
            Assert.Equal(EfetivacaoEsusSg.Efetivado, sVeio.Efetivacao);
            Assert.Equal(t0, sVeio.EfetivadoEm);
            Assert.Contains(sVeio.Eventos, e => e.TipoEvento == TipoEventoExterno.ChegadaNoDestino);

            var sFaltou = await db.EsusSgSolicitacoes.FirstAsync(x => x.IdEsusSg == faltou);
            Assert.Equal(EfetivacaoEsusSg.NaoEfetivado, sFaltou.Efetivacao);
            Assert.Equal("Não Compareceu", sFaltou.MotivoNaoEfetivacao);

            var sAberto = await db.EsusSgSolicitacoes.FirstAsync(x => x.IdEsusSg == aberto);
            Assert.Null(sAberto.Efetivacao);
            Assert.NotNull(sAberto.EfetivacaoLidaEm); // olhou depois do dia: a unidade não apontou

            Assert.Null((await db.EsusSgSolicitacoes.FirstAsync(x => x.IdEsusSg == outro)).Efetivacao);
            Assert.Null((await db.EsusSgSolicitacoes.FirstAsync(x => x.IdEsusSg == futuro)).EfetivacaoLidaEm);
        }
        Assert.DoesNotContain("p" + futuro, leitor.HistoricosLidos);

        // Segunda rodada: quem já foi efetivado não é relido; os outros são.
        leitor.HistoricosLidos.Clear();
        await using (var db = fixture.CriarDbContext()) await RodarAsync(db, leitor);
        Assert.DoesNotContain("p" + veio, leitor.HistoricosLidos);
        Assert.Contains("p" + aberto, leitor.HistoricosLidos);
        await using (var db = fixture.CriarDbContext())
        {
            Assert.Equal(1, await db.EsusSgEventos.CountAsync(e =>
                e.EsusSgSolicitacao!.IdEsusSg == veio && e.TipoEvento == TipoEventoExterno.ChegadaNoDestino));
        }
    }

    [Fact]
    public async Task Agendado_muda_situacao_e_quem_some_das_duas_listas_sai_da_fila()
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var (fica, agenda, some) = (Id(), Id(), Id());
        var leitor = new LeitorFake { Fila = [NaFila(fica), NaFila(agenda), NaFila(some)] };

        await using (var db = fixture.CriarDbContext()) await RodarAsync(db, leitor);

        leitor.Fila = [NaFila(fica, prioridade: "URGENTE")];
        leitor.Agendados = [Agendado(agenda, "x1", hoje.AddDays(15))];
        await using (var db = fixture.CriarDbContext()) await RodarAsync(db, leitor);

        await using (var db = fixture.CriarDbContext())
        {
            var sFica = await db.EsusSgSolicitacoes.Include(x => x.Eventos).FirstAsync(x => x.IdEsusSg == fica);
            Assert.Equal(SituacaoEsusSg.EmFila, sFica.Situacao);
            Assert.Contains(sFica.Eventos, e => e.Evento == "Mudança de prioridade" && e.EstadoAtual == "URGENTE");
            Assert.True(await db.EsusSgGatilhos.AnyAsync(g => g.IdEsusSg == fica && g.Tipo == TipoGatilhoEsusSg.MudancaPrioridade));

            var sAgenda = await db.EsusSgSolicitacoes.FirstAsync(x => x.IdEsusSg == agenda);
            Assert.Equal(SituacaoEsusSg.Agendada, sAgenda.Situacao);
            Assert.Equal(SituacaoEsusSg.EmFila, sAgenda.SituacaoAnterior);
            Assert.Null(sAgenda.PosicaoFila);

            var sSome = await db.EsusSgSolicitacoes.Include(x => x.Eventos).FirstAsync(x => x.IdEsusSg == some);
            Assert.Equal(SituacaoEsusSg.SaiuDaFila, sSome.Situacao);
            Assert.Contains(sSome.Eventos, e => e.Evento == "Saiu da fila");
        }
    }

    [Fact]
    public async Task Leitura_incompleta_da_fila_nao_acusa_saida_e_a_rodada_fica_parcial()
    {
        var some = Id();
        var leitor = new LeitorFake { Fila = [NaFila(some)] };
        await using (var db = fixture.CriarDbContext()) await RodarAsync(db, leitor);

        // A fila "voltou" sem o pedido, mas a conta não fechou: pode ser falha de leitura.
        leitor.Fila = [];
        leitor.FilaIncompleta = true;
        Guid execId;
        await using (var db = fixture.CriarDbContext()) execId = await RodarAsync(db, leitor);

        await using (var db = fixture.CriarDbContext())
        {
            var s = await db.EsusSgSolicitacoes.FirstAsync(x => x.IdEsusSg == some);
            Assert.Equal(SituacaoEsusSg.EmFila, s.Situacao);
            var exec = await db.EsusSgVarreduraExecucoes.FirstAsync(x => x.Id == execId);
            Assert.Equal(StatusVarreduraEsusSg.Parcial, exec.Status);
        }
    }

    [Fact]
    public async Task Troca_de_sessao_principal_nao_e_reagendamento_mas_data_que_some_e()
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var id = Id();
        var leitor = new LeitorFake
        {
            Agendados = [Agendado(id, "s1", hoje.AddDays(5)), Agendado(id, "s2", hoje.AddDays(35))],
        };
        await using (var db = fixture.CriarDbContext()) await RodarAsync(db, leitor);

        // A sessão s2 foi remarcada de +35 para +50 dias: a data que tínhamos como principal (s1)
        // continua no conjunto → não é remarcação do principal.
        leitor.Agendados = [Agendado(id, "s1", hoje.AddDays(5)), Agendado(id, "s2", hoje.AddDays(50))];
        await using (var db = fixture.CriarDbContext()) await RodarAsync(db, leitor);
        await using (var db = fixture.CriarDbContext())
        {
            Assert.False(await db.EsusSgGatilhos.AnyAsync(g => g.IdEsusSg == id && g.Tipo == TipoGatilhoEsusSg.MudancaAgendamento));
        }

        // Agora a sessão principal (s1) muda de data: remarcação de verdade.
        leitor.Agendados = [Agendado(id, "s1", hoje.AddDays(8)), Agendado(id, "s2", hoje.AddDays(50))];
        await using (var db = fixture.CriarDbContext()) await RodarAsync(db, leitor);
        await using (var db = fixture.CriarDbContext())
        {
            Assert.True(await db.EsusSgGatilhos.AnyAsync(g => g.IdEsusSg == id && g.Tipo == TipoGatilhoEsusSg.MudancaAgendamento));
            var s = await db.EsusSgSolicitacoes.FirstAsync(x => x.IdEsusSg == id);
            Assert.Equal(hoje.AddDays(8), s.DataAgendada);
        }
    }
}

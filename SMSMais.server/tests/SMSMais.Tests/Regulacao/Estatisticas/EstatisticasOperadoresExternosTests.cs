using Microsoft.Extensions.Caching.Memory;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Regulacao.Estatisticas;
using SMSMais.Data;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Estatisticas;

/// <summary>
/// Estatísticas dos operadores do SER/SERNIT (20/09/2026). O nome mora em <c>usuario</c> do evento,
/// a hora é de Brasília (a coluna é timestamptz) e a conta é SQL de verdade — por isso contra banco.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class EstatisticasOperadoresExternosTests(PostgresFixture fixture)
{
    private sealed record Cenario(DateOnly De, DateOnly Ate, string NomeA, string NomeB, string NomeC);

    /// <summary>Instante UTC de um horário de Brasília (UTC−3) num dia.</summary>
    private static DateTime Brasilia(DateOnly dia, int hora, int minuto = 0) =>
        DateTime.SpecifyKind(dia.ToDateTime(new TimeOnly(hora, minuto)), DateTimeKind.Utc).AddHours(3);

    private static SerSolicitacao Solicitacao(int n, int i, DateOnly dataSolicitacao, string recurso) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            IdSer = $"EST{n}{i:D3}",
            Tipo = TipoRecursoSer.Consulta,
            Recurso = recurso,
            DataSolicitacao = dataSolicitacao,
            PacienteNome = "PACIENTE DE TESTE",
            Situacao = SituacaoSer.EmFila,
            UnidadeExecutora = $"UNIDADE EST {n}",
            SincronizadoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };

    private static SerEvento Evento(
        SerSolicitacao s, string usuario, DateTime quando, TipoEventoExterno tipo, string lotacao = "Gestor: TESTE") =>
        new()
        {
            Id = Guid.CreateVersion7(),
            SerSolicitacaoId = s.Id,
            DataEvento = quando,
            Evento = tipo.ToString(),
            TipoEvento = tipo,
            Usuario = usuario,
            LotacaoEvento = lotacao,
            CapturadoEm = DateTime.UtcNow,
        };

    /// <summary>
    /// Período de 30 dias num ano distante e nomes únicos por execução: a bancada é compartilhada e
    /// os testes deixam linhas para trás.
    /// </summary>
    private static async Task<Cenario> MontarAsync(SmsMaisDbContext db)
    {
        var n = Random.Shared.Next(100_000, 999_999);
        var de = new DateOnly(2150 + Random.Shared.Next(0, 40), 3, 1);
        var ate = de.AddDays(29);
        string a = $"OPERADORA EST {n} A", b = $"operadora est {n} b", c = $"UNIDADE EST {n} C";

        var s1 = Solicitacao(n, 1, de.AddDays(-4), "CONSULTA EM TESTE A");
        var s2 = Solicitacao(n, 2, de.AddDays(-10), "CONSULTA EM TESTE B");
        var s3 = Solicitacao(n, 3, de.AddDays(-1), "CONSULTA EM TESTE A");
        db.SerSolicitacoes.AddRange(s1, s2, s3);

        // A: dia 0 → 8h agenda s1 (espera 4 d), 9h FollowUP s2, 17h30 cancela s3 = 3 ações;
        //    dia 1 → 10h agenda s2 (espera 11 d) = 1 ação; dia 2 (sábado? não importa) → 21h pendencia s3.
        //    5 ações, 3 dias, pico 3, 2 agendamentos (espera mediana 7,5 d), 1 fora do expediente.
        db.SerEventos.AddRange(
            Evento(s1, a, Brasilia(de, 8), TipoEventoExterno.Agendar),
            Evento(s2, a, Brasilia(de, 9), TipoEventoExterno.FollowUp),
            Evento(s3, a, Brasilia(de, 17, 30), TipoEventoExterno.Cancelar),
            Evento(s2, a, Brasilia(de.AddDays(1), 10), TipoEventoExterno.Agendar),
            Evento(s3, a, Brasilia(de.AddDays(2), 21), TipoEventoExterno.Pendenciar));
        // B (nome em minúsculas no dado cru — normalização): 2 chegadas no dia 0.
        db.SerEventos.AddRange(
            Evento(s1, b, Brasilia(de, 14), TipoEventoExterno.ChegadaNoDestino, "Operador da Central: TESTE"),
            Evento(s2, b, Brasilia(de, 15), TipoEventoExterno.ChegadaNoDestino, "Operador da Central: TESTE"));
        // C não habilitado: 4 ações que só entram no total geral.
        db.SerEventos.AddRange(
            Evento(s1, c, Brasilia(de, 11), TipoEventoExterno.Solicitar),
            Evento(s2, c, Brasilia(de, 11), TipoEventoExterno.Solicitar),
            Evento(s3, c, Brasilia(de, 11), TipoEventoExterno.Solicitar),
            Evento(s3, c, Brasilia(de.AddDays(3), 11), TipoEventoExterno.FollowUp));
        // Período anterior (mesmo tamanho, logo antes): 2 de A. E uma às 23h de Brasília do último dia
        // anterior — em UTC já é o dia seguinte; tem de continuar no período anterior.
        db.SerEventos.AddRange(
            Evento(s1, a, Brasilia(de.AddDays(-5), 9), TipoEventoExterno.FollowUp),
            Evento(s1, a, Brasilia(de.AddDays(-1), 23), TipoEventoExterno.FollowUp));

        await db.SaveChangesAsync();
        return new Cenario(de, ate, a.ToUpperInvariant(), b.ToUpperInvariant(), c.ToUpperInvariant());
    }

    private static EstatisticasOperadoresExternosService Servico(SmsMaisDbContext db) =>
        new(db, null!, new MemoryCache(new MemoryCacheOptions()));

    private static HashSet<string> Habilitados(params string[] nomes) => new(nomes, StringComparer.Ordinal);

    [Fact]
    public async Task Equipe_conta_acoes_por_nome_em_dias_de_brasilia_e_separa_os_verbos()
    {
        await using var db = fixture.CriarDbContext();
        var c = await MontarAsync(db);

        var r = await Servico(db).EquipeAsync(FonteEstatisticaExterna.Ser, c.De, c.Ate, Habilitados(c.NomeA, c.NomeB));

        Assert.Equal("SER", r.Fonte);
        Assert.Equal(30, r.DiasCorridos);
        Assert.Equal(2, r.Ranking.Count);

        var a = r.Ranking[0];
        Assert.Equal($"o:{c.NomeA}", a.Chave);
        Assert.Equal(c.NomeA, a.Nome);
        Assert.Equal(5, a.Acoes);
        Assert.Equal(2, a.Agendamentos);
        Assert.Equal(1, a.Cancelamentos);
        Assert.Equal(1, a.FollowUps);
        Assert.Equal(1, a.Pendencias);
        Assert.Equal(3, a.Solicitacoes);
        Assert.Equal(3, a.DiasTrabalhados);
        Assert.Equal(3, a.PicoDiario);
        Assert.Equal(c.De, a.DiaDoPico);
        Assert.Equal(7.5, a.EsperaMedianaDias);
        Assert.Equal(2, a.Recursos);
        Assert.Equal(["Gestor: TESTE"], a.Lotacoes);
        Assert.NotNull(a.HoraInicioMediana);
        Assert.Equal(10, a.HoraInicioMediana);          // mediana de {8, 10, 21}
        Assert.Equal(17.5, a.HoraFimMediana);           // mediana de {17,5; 10; 21}

        var b = r.Ranking[1];
        Assert.Equal(c.NomeB, b.Nome);                  // normalizado em maiúsculas
        Assert.Equal(2, b.Acoes);
        Assert.Equal(0, b.Agendamentos);

        Assert.Equal(7, r.Atual.Acoes);
        Assert.Equal(2, r.Atual.Agendamentos);
        Assert.Equal(2, r.Atual.Operadores);
        Assert.Equal(3, r.Atual.Solicitacoes);
        Assert.Equal(7.5, r.Atual.EsperaMedianaDias);
        Assert.Equal(14.3, r.Atual.PercentualForaDoExpediente);   // 1 de 7 (a pendência das 21h)
        Assert.Equal(2, r.Anterior.Acoes);                          // inclui a das 23h de Brasília
        Assert.True(r.AcoesTodosOsOperadores >= 11);               // C entra só aqui
        Assert.Equal(100.0, r.ConcentracaoTop3Percentual);

        var dia0 = Assert.Single(r.PorDia, x => x.Dia == c.De);
        Assert.Equal(5, dia0.Acoes);
        Assert.Equal(2, dia0.Operadores);
        Assert.Equal(1, dia0.Agendamentos);
        Assert.Equal(7, r.PorDiaSemana.Count);
        Assert.Equal(24, r.PorHora.Count);
        Assert.Equal(1, r.PorHora[21].Acoes);
        Assert.Contains(r.PorTipoEvento, t => t.Rotulo == "Agendamento" && t.Acoes == 2);
        Assert.Contains(r.PorTipoEvento, t => t.Rotulo == "Chegada no destino" && t.Acoes == 2 && t.Operadores == 1);
        Assert.Contains(r.TopRecursos, t => t.Rotulo == "CONSULTA EM TESTE A" && t.Acoes == 4);
        Assert.Contains(r.TopLotacoes, t => t.Rotulo == "Gestor: TESTE" && t.Acoes == 5);
    }

    [Fact]
    public async Task Sem_ninguem_habilitado_nao_ha_ranking()
    {
        await using var db = fixture.CriarDbContext();
        var c = await MontarAsync(db);

        var r = await Servico(db).EquipeAsync(FonteEstatisticaExterna.Ser, c.De, c.Ate, Habilitados());

        Assert.Empty(r.Ranking);
        Assert.Equal(0, r.Atual.Acoes);
        Assert.Equal(0, r.Habilitados);
    }

    [Fact]
    public async Task Individual_bate_com_a_linha_do_ranking_e_recusa_quem_nao_esta_habilitado()
    {
        await using var db = fixture.CriarDbContext();
        var c = await MontarAsync(db);
        var habilitados = Habilitados(c.NomeA, c.NomeB);
        var servico = Servico(db);

        var r = await servico.IndividualAsync(FonteEstatisticaExterna.Ser, $"o:{c.NomeA}", c.De, c.Ate, habilitados);

        Assert.Equal(5, r.Metricas!.Acoes);
        Assert.Equal(3, r.PorDia.Count);
        Assert.Equal(2, r.Anterior.Acoes);
        Assert.Equal(c.NomeA, r.Nome);

        await Assert.ThrowsAsync<NaoEncontradoException>(() =>
            servico.IndividualAsync(FonteEstatisticaExterna.Ser, $"o:{c.NomeC}", c.De, c.Ate, habilitados));
    }

    [Fact]
    public async Task Sernit_le_as_tabelas_proprias_e_nao_ve_o_que_e_do_ser()
    {
        await using var db = fixture.CriarDbContext();
        var c = await MontarAsync(db);
        var n = Random.Shared.Next(100_000, 999_999);

        var s = new SernitSolicitacao
        {
            Id = Guid.CreateVersion7(),
            IdSernit = $"N{n}",
            Tipo = TipoRecursoSernit.Exame,
            Recurso = "EXAME EM TESTE",
            DataSolicitacao = c.De.AddDays(-2),
            PacienteNome = "PACIENTE DE TESTE",
            Situacao = SituacaoSernit.EmFila,
            SincronizadoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        db.SernitSolicitacoes.Add(s);
        db.SernitEventos.Add(new SernitEvento
        {
            Id = Guid.CreateVersion7(),
            SernitSolicitacaoId = s.Id,
            DataEvento = Brasilia(c.De, 9),
            Evento = "Agendar",
            TipoEvento = TipoEventoExterno.Agendar,
            Usuario = c.NomeA,
            CapturadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var r = await Servico(db).EquipeAsync(FonteEstatisticaExterna.Sernit, c.De, c.Ate, Habilitados(c.NomeA));

        Assert.Equal("SERNIT", r.Fonte);
        var a = Assert.Single(r.Ranking);
        Assert.Equal(1, a.Acoes);           // só o evento do SERNIT; os 5 do SER ficam de fora
        Assert.Equal(2, a.EsperaMedianaDias);
    }

    [Fact]
    public async Task Periodo_maior_que_um_ano_e_recusado()
    {
        await using var db = fixture.CriarDbContext();

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            Servico(db).EquipeAsync(FonteEstatisticaExterna.Ser, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2), Habilitados()));
    }
}

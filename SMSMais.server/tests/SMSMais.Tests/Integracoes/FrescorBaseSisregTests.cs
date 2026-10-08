using FluentAssertions;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.Sisreg.Base;
using SMSMais.Core.Integracoes.SisregWeb.Indicadores;
using SMSMais.Core.Integracoes.SisregWeb.Varredura;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Conferência de frescor da base do SISREG. O que está sob teste é o que faz o "Pendente de
/// atualização" mentir: dia passado sem a lista de faltas lida, leitura recente que parou de ser
/// renovada, e unidade cuja chegada a varredura deixou de reler. Caso real: 14/09/2026 ficou sem
/// falta nenhuma e o relatório de pendentes da fisioterapia saiu com 61 faltas contadas como pendentes.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class FrescorBaseSisregTests(PostgresFixture fixture)
{
    private static readonly DateOnly Hoje = FusoBrasilia.HojeEmBrasilia();

    private static FrescorBaseSisregService Servico(SmsMaisDbContext db) =>
        new(db, Options.Create(new FrescorBaseSisregOpcoes()), Options.Create(new ColetaIndicadoresOpcoes()),
            Options.Create(new VarreduraSisregOpcoes()));

    /// <summary>Unidade nova a cada teste: a bancada é compartilhada.</summary>
    private static async Task<Guid> UnidadeAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE FRESCOR {Guid.NewGuid():N}"[..40],
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        return unidade.Id;
    }

    /// <summary>Um agendamento do SISREG às 10h de Brasília do dia, passado e sem chegada confirmada.</summary>
    private static Solicitacao Agendamento(SmsMaisDbContext db, Guid unidade, DateOnly dia, DateTime? chegadaLidaEm = null)
    {
        var s = new Solicitacao
        {
            Id = Guid.NewGuid(),
            PacienteId = Guid.NewGuid(),
            Categoria = CategoriaSolicitacao.Consulta,
            UnidadeExecutanteId = unidade,
            SolicitanteNome = "DR TESTE",
            ProcedimentoTexto = "FISIOTERAPIA DE TESTE",
            Status = StatusSolicitacao.Agendada,
            StatusConfirmacao = StatusConfirmacaoAgendamento.Pendente,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            DataAgendada = dia.ToDateTime(new TimeOnly(13, 0), DateTimeKind.Utc),
            DataSolicitacao = dia.AddDays(-30),
            CodigoSolicitacao = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString(),
            RawSisreg = "x",
            ChegadaConfirmadaSisreg = chegadaLidaEm is null ? null : false,
            ChegadaSisregLidaEm = chegadaLidaEm,
            CriadoEm = DateTime.UtcNow,
        };
        db.Solicitacoes.Add(s);
        return s;
    }

    /// <summary>Janela de faltas lida. Escopo único: o índice (coletor, início, escopo) é da bancada inteira.</summary>
    private static void JanelaLida(SmsMaisDbContext db, ColetorIndicadorSisreg coletor, DateOnly inicio, DateOnly fim, DateTime lidoEm) =>
        db.SisregIndicadorColetas.Add(new SisregIndicadorColeta
        {
            Id = Guid.NewGuid(), Coletor = coletor, JanelaInicio = inicio, JanelaFim = fim,
            Escopo = $"t{Guid.NewGuid():N}"[..20], Status = StatusColetaIndicador.Concluida,
            Linhas = 1, IniciadoEm = lidoEm, LidoEm = lidoEm, CriadoEm = DateTime.UtcNow,
        });

    /// <summary>Um dia por teste, num ano que nenhuma outra janela da bancada cobre.</summary>
    private static DateOnly DiaAntigo() => new DateOnly(2012, 1, 1).AddDays(Random.Shared.Next(0, 360));

    [Fact]
    public async Task Dia_passado_sem_lista_de_faltas_aparece_e_dia_lido_nao()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        var semLista = DiaAntigo();
        var lido = semLista.AddDays(1);
        Agendamento(db, unidade, semLista);
        Agendamento(db, unidade, semLista);
        Agendamento(db, unidade, lido);
        // Leitura oficial é definitiva: lida uma vez, está lida, por mais velha que seja.
        JanelaLida(db, ColetorIndicadorSisreg.Faltas, lido, lido, DateTime.UtcNow.AddYears(-10));
        await db.SaveChangesAsync();

        var f = await Servico(db).ConferirAsync(semLista, lido, [unidade], default);

        f.DiasSemFaltas.Should().ContainSingle()
            .Which.Should().Be(new DiaSemFaltasDto(semLista, 2, null));
        f.EmDia.Should().BeFalse();
    }

    /// <summary>
    /// A leitura das semanas recentes é renovada de hora em hora. Se parou (coletor travado, SISREG
    /// cortando sempre), a lista gravada envelhece e a unidade segue apontando falta — conta como atraso.
    /// </summary>
    [Fact]
    public async Task Leitura_recente_que_parou_de_ser_renovada_conta_como_atrasada()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        var parado = Hoje.AddDays(-5);
        var renovado = Hoje.AddDays(-6);
        Agendamento(db, unidade, parado);
        Agendamento(db, unidade, renovado);
        JanelaLida(db, ColetorIndicadorSisreg.FaltasRecentes, parado, parado, DateTime.UtcNow.AddHours(-50));
        JanelaLida(db, ColetorIndicadorSisreg.FaltasRecentes, renovado, renovado, DateTime.UtcNow.AddMinutes(-40));
        await db.SaveChangesAsync();

        var f = await Servico(db).ConferirAsync(renovado, parado, [unidade], default);

        f.DiasSemFaltas.Select(d => d.Dia).Should().Contain(parado).And.NotContain(renovado);
    }

    [Fact]
    public async Task Unidade_sem_releitura_de_chegada_aparece_e_a_relida_nao()
    {
        await using var db = fixture.CriarDbContext();
        var atrasada = await UnidadeAsync(db);
        var emDia = await UnidadeAsync(db);
        var dia = Hoje.AddDays(-3);
        Agendamento(db, atrasada, dia, chegadaLidaEm: DateTime.UtcNow.AddHours(-60));
        Agendamento(db, atrasada, dia, chegadaLidaEm: DateTime.UtcNow.AddHours(-55));
        Agendamento(db, emDia, dia, chegadaLidaEm: DateTime.UtcNow.AddHours(-3));
        await db.SaveChangesAsync();

        var f = await Servico(db).ConferirAsync(dia, dia, [atrasada, emDia], default);

        var linha = f.ChegadasAtrasadas.Should().ContainSingle().Which;
        linha.UnidadeId.Should().Be(atrasada);
        linha.Agendamentos.Should().Be(2);
        linha.UltimaLeitura.Should().BeCloseTo(DateTime.UtcNow.AddHours(-55), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Hoje_e_futuro_nao_entram_na_conferencia()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        Agendamento(db, unidade, Hoje);
        Agendamento(db, unidade, Hoje.AddDays(3));
        await db.SaveChangesAsync();

        var f = await Servico(db).ConferirAsync(Hoje, Hoje.AddDays(5), [unidade], default);

        f.EmDia.Should().BeTrue();
    }

    [Fact]
    public void Aviso_diz_o_dia_quantos_estao_em_aberto_e_desde_quando()
    {
        var f = new FrescorBaseSisregDto(
            [new DiaSemFaltasDto(new DateOnly(2026, 9, 14), 875, null),
             new DiaSemFaltasDto(new DateOnly(2026, 9, 12), 32, new DateTime(2026, 10, 5, 19, 52, 0, DateTimeKind.Utc))],
            [new ChegadaAtrasadaDto(Guid.NewGuid(), "CDT", 120, new DateTime(2026, 10, 5, 21, 0, 0, DateTimeKind.Utc))],
            30);

        var (titulo, detalhe) = AvisoFrescorBaseSisregWorker.Montar(f);

        titulo.Should().Be("FALHOU a atualização das faltas do SISREG em 2 dia(s) e FALHOU a releitura de chegada em 1 unidade(s)");
        detalhe.Should().Contain("Faltas não atualizadas: 14/09 (875 em aberto, nunca lido)")
            .And.Contain("12/09 (32 em aberto, última leitura 05/10 16:52)")
            .And.Contain("CDT (120 em aberto, desde 05/10 18:00)")
            .And.Contain("Pendente de atualização");
    }

    private static HashSet<string> P(params string[] p) => [.. p];

    /// <summary>
    /// Quando o celular toca: resumo na primeira conferência do dia; depois só pendência NOVA; e um
    /// aviso quando volta ao normal. A mesma pendência não repete de hora em hora.
    /// </summary>
    [Fact]
    public void Aviso_sai_no_resumo_do_dia_em_pendencia_nova_e_quando_normaliza()
    {
        var d = AvisoFrescorBaseSisregWorker.DecisaoAviso.Avisar;
        var nada = AvisoFrescorBaseSisregWorker.DecisaoAviso.Nada;
        var ok = AvisoFrescorBaseSisregWorker.DecisaoAviso.AvisarQueNormalizou;

        // Primeira hora do dia, com pendência já avisada ontem: resumo.
        AvisoFrescorBaseSisregWorker.Decidir(P("faltas:2026-09-14"), P("faltas:2026-09-14"), primeiraDoDia: true).Should().Be(d);
        // Mesma pendência nas horas seguintes: silêncio.
        AvisoFrescorBaseSisregWorker.Decidir(P("faltas:2026-09-14"), P("faltas:2026-09-14"), primeiraDoDia: false).Should().Be(nada);
        // Apareceu um dia novo: avisa na hora.
        AvisoFrescorBaseSisregWorker.Decidir(P("faltas:2026-09-14", "faltas:2026-10-07"), P("faltas:2026-09-14"), primeiraDoDia: false).Should().Be(d);
        // Um resolveu e o outro continua: silêncio.
        AvisoFrescorBaseSisregWorker.Decidir(P("faltas:2026-10-07"), P("faltas:2026-09-14", "faltas:2026-10-07"), primeiraDoDia: false).Should().Be(nada);
        // Tudo lido depois de um aviso: "voltou ao normal".
        AvisoFrescorBaseSisregWorker.Decidir(P(), P("faltas:2026-10-07"), primeiraDoDia: false).Should().Be(ok);
        // Nada pendente e nada avisado: silêncio.
        AvisoFrescorBaseSisregWorker.Decidir(P(), P(), primeiraDoDia: true).Should().Be(nada);
    }
}

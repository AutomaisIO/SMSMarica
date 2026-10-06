using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMSMais.Core.Integracoes.SisregWeb.Indicadores;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// O cursor do coletor dos Indicadores (<c>sisreg_indicador_coleta</c>) contra o Postgres: a tentativa
/// conta ANTES da chamada, o item órfão em "em andamento" volta a pendente, e o plano não duplica janela.
/// Janelas em 2030–2031 para não cruzar com outros testes do mesmo banco.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ColetaIndicadoresCursorTests(PostgresFixture fixture)
{
    private static readonly DateOnly InicioDoTeste = new(2030, 1, 1);

    private static ColetaIndicadoresSisregService Criar(SmsMaisDbContext db) =>
        new(db, Options.Create(new ColetaIndicadoresOpcoes()));

    private static Task LimparAsync(SmsMaisDbContext db) =>
        db.SisregIndicadorColetas.Where(c => c.JanelaInicio >= InicioDoTeste).ExecuteDeleteAsync();

    [Fact]
    public async Task Reserva_conta_a_tentativa_antes_e_o_orfao_em_andamento_volta_a_pendente()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);
        var id = Guid.NewGuid();
        db.SisregIndicadorColetas.Add(new SisregIndicadorColeta
        {
            Id = id, Coletor = ColetorIndicadorSisreg.Ppi, JanelaInicio = InicioDoTeste,
            JanelaFim = InicioDoTeste.AddMonths(1).AddDays(-1), Status = StatusColetaIndicador.Pendente,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var item = await Criar(db).ReservarProximoAsync(default);

        Assert.Equal(id, item?.Id);
        await using (var ver = fixture.CriarDbContext())
        {
            var gravado = await ver.SisregIndicadorColetas.SingleAsync(c => c.Id == id);
            Assert.Equal(StatusColetaIndicador.EmAndamento, gravado.Status);
            Assert.Equal(1, gravado.Tentativas);
            Assert.NotNull(gravado.IniciadoEm);
        }

        // Restart no meio do item: ninguém mais o executa — volta a pendente, a tentativa fica contada.
        await Criar(db).ResetarEmAndamentoAsync(null, default);
        await using (var ver = fixture.CriarDbContext())
        {
            var gravado = await ver.SisregIndicadorColetas.SingleAsync(c => c.Id == id);
            Assert.Equal(StatusColetaIndicador.Pendente, gravado.Status);
            Assert.Equal(1, gravado.Tentativas);
        }

        await LimparAsync(db);
    }

    [Fact]
    public async Task Plano_cria_as_janelas_recentes_uma_vez_so()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);
        var hoje = new DateOnly(2031, 6, 10);

        await Criar(db).PlanejarAsync(hoje, default);
        await using (var db2 = fixture.CriarDbContext()) await Criar(db2).PlanejarAsync(hoje, default);

        await using var ver = fixture.CriarDbContext();
        var itens = await ver.SisregIndicadorColetas.Where(c => c.JanelaInicio >= InicioDoTeste).ToListAsync();
        // Faltas: semanas terminadas até 11/05 (30 dias) de mar–mai. PPI, canceladas e lista de unidades: mar, abr, mai.
        Assert.Equal(9, itens.Count(c => c.Coletor == ColetorIndicadorSisreg.Faltas));
        Assert.Equal(3, itens.Count(c => c.Coletor == ColetorIndicadorSisreg.Ppi));
        Assert.Equal(3, itens.Count(c => c.Coletor == ColetorIndicadorSisreg.Canceladas));
        Assert.Equal(3, itens.Count(c => c.Coletor == ColetorIndicadorSisreg.Desfechos && c.Escopo == TrabalhoUnidades.Escopo));
        Assert.All(itens, c => Assert.Equal(StatusColetaIndicador.Pendente, c.Status));

        await LimparAsync(db);
    }

    /// <summary>
    /// As semanas recentes de faltas são ROTINA: fecham, e voltam a pendente quando a leitura envelhece
    /// (é o "de hora em hora"), com as tentativas zeradas. A semana corrente cresce um dia por dia. E
    /// quando a semana ganha a leitura oficial, a provisória sai de cena.
    /// </summary>
    [Fact]
    public async Task Faltas_recentes_sao_relidas_quando_envelhecem_e_somem_quando_a_oficial_fecha()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);
        var hoje = new DateOnly(2031, 6, 10);

        await Criar(db).PlanejarAsync(hoje, default);

        // Limite = 11/05: de 09–16/05 até a semana corrente, cortada em ontem (09/06).
        var recentes = await db.SisregIndicadorColetas.AsNoTracking()
            .Where(c => c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.JanelaInicio >= InicioDoTeste)
            .OrderBy(c => c.JanelaInicio).ToListAsync();
        DateOnly[] inicios =
            [new(2031, 5, 9), new(2031, 5, 17), new(2031, 5, 24), new(2031, 6, 1), new(2031, 6, 9)];
        Assert.Equal(inicios, recentes.Select(c => c.JanelaInicio));
        Assert.Equal(new DateOnly(2031, 6, 9), recentes[^1].JanelaFim);

        // Todas lidas agora há pouco, menos uma lida há duas horas.
        var velha = recentes[1].Id;
        await db.SisregIndicadorColetas.Where(c => c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.JanelaInicio >= InicioDoTeste)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.Status, StatusColetaIndicador.Concluida)
                .SetProperty(c => c.Tentativas, 3)
                .SetProperty(c => c.IniciadoEm, DateTime.UtcNow)
                .SetProperty(c => c.LidoEm, DateTime.UtcNow));
        await db.SisregIndicadorColetas.Where(c => c.Id == velha)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.IniciadoEm, DateTime.UtcNow.AddHours(-2)));

        await using (var db2 = fixture.CriarDbContext()) await Criar(db2).PlanejarAsync(hoje, default);

        await using (var ver = fixture.CriarDbContext())
        {
            var depois = await ver.SisregIndicadorColetas
                .Where(c => c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.JanelaInicio >= InicioDoTeste).ToListAsync();
            var rearmada = Assert.Single(depois, c => c.Status == StatusColetaIndicador.Pendente);
            Assert.Equal(velha, rearmada.Id);
            Assert.Equal(0, rearmada.Tentativas);
        }

        // No dia seguinte a semana corrente ganha o dia 10 — e volta a pendente mesmo recém-lida, porque
        // a leitura que existe não cobre o dia novo.
        await using (var db3 = fixture.CriarDbContext()) await Criar(db3).PlanejarAsync(hoje.AddDays(1), default);
        await using (var ver = fixture.CriarDbContext())
        {
            var corrente = await ver.SisregIndicadorColetas.SingleAsync(c =>
                c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.JanelaInicio == new DateOnly(2031, 6, 9));
            Assert.Equal(new DateOnly(2031, 6, 10), corrente.JanelaFim);
            Assert.Equal(StatusColetaIndicador.Pendente, corrente.Status);
        }

        // Em 16/06 a semana 09–16/05 completa 30 dias e entra no plano OFICIAL. Enquanto a oficial não
        // fecha, a provisória continua lá (é ela que ainda cobre a semana)…
        var dia30 = new DateOnly(2031, 6, 16);
        await using (var db4 = fixture.CriarDbContext()) await Criar(db4).PlanejarAsync(dia30, default);
        await using (var ver = fixture.CriarDbContext())
        {
            Assert.True(await ver.SisregIndicadorColetas.AnyAsync(c =>
                c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.JanelaInicio == new DateOnly(2031, 5, 9)));

            var oficial = await ver.SisregIndicadorColetas.SingleAsync(c =>
                c.Coletor == ColetorIndicadorSisreg.Faltas && c.JanelaInicio == new DateOnly(2031, 5, 9));
            oficial.Status = StatusColetaIndicador.Concluida;
            oficial.LidoEm = DateTime.UtcNow;
            await ver.SaveChangesAsync();
        }

        // …e some no plano seguinte ao fechamento da oficial.
        await using (var db5 = fixture.CriarDbContext()) await Criar(db5).PlanejarAsync(dia30, default);
        await using (var ver = fixture.CriarDbContext())
        {
            Assert.False(await ver.SisregIndicadorColetas.AnyAsync(c =>
                c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.JanelaInicio == new DateOnly(2031, 5, 9)));
        }

        await LimparAsync(db);
    }

    /// <summary>
    /// Semana recente que estourou o tempo do SISREG é dividida em dias e NÃO volta a ser tentada
    /// inteira: quem reabre de hora em hora são os dias, e a semana corrente ganha o dia novo. Antes a
    /// semana cheia era tentada a cada hora e nunca fechava (09–16/09 e 17–23/09 de 2026).
    /// </summary>
    [Fact]
    public async Task Faltas_recentes_divididas_reabrem_os_dias_e_nao_a_semana()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);
        var hoje = new DateOnly(2031, 6, 10);
        await Criar(db).PlanejarAsync(hoje, default);

        Guid IdDaSemana(SmsMaisDbContext d, DateOnly inicio) => d.SisregIndicadorColetas
            .Single(c => c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.Escopo == "" && c.JanelaInicio == inicio).Id;

        var cheia = new DateOnly(2031, 5, 17);
        var corrente = new DateOnly(2031, 6, 9);
        await using (var d = fixture.CriarDbContext())
        {
            await Criar(d).DividirEmDiasAsync(IdDaSemana(d, cheia), "tempo esgotado", default);
            await Criar(d).DividirEmDiasAsync(IdDaSemana(d, corrente), "tempo esgotado", default);
        }

        // Tudo lido há duas horas — a semana e os dias envelheceram.
        await db.SisregIndicadorColetas
            .Where(c => c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.JanelaInicio >= InicioDoTeste
                        && c.Escopo == PlanoColetaIndicadores.EscopoDia)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.Status, StatusColetaIndicador.Concluida)
                .SetProperty(c => c.Tentativas, 2)
                .SetProperty(c => c.IniciadoEm, DateTime.UtcNow.AddHours(-2)));
        await db.SisregIndicadorColetas
            .Where(c => c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.JanelaInicio >= InicioDoTeste && c.Escopo == "")
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.IniciadoEm, DateTime.UtcNow.AddHours(-2)));

        await using (var d = fixture.CriarDbContext()) await Criar(d).PlanejarAsync(hoje, default);
        await using (var ver = fixture.CriarDbContext())
        {
            var semana = await ver.SisregIndicadorColetas.SingleAsync(c =>
                c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.Escopo == "" && c.JanelaInicio == cheia);
            Assert.Equal(StatusColetaIndicador.Falha, semana.Status);
            Assert.StartsWith(ColetaIndicadoresSisregService.MarcaDividida, semana.Erro);

            var dias = await ver.SisregIndicadorColetas
                .Where(c => c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.Escopo == PlanoColetaIndicadores.EscopoDia
                            && c.JanelaInicio >= cheia && c.JanelaInicio <= new DateOnly(2031, 5, 23))
                .ToListAsync();
            Assert.Equal(7, dias.Count);
            Assert.All(dias, x =>
            {
                Assert.Equal(StatusColetaIndicador.Pendente, x.Status);
                Assert.Equal(0, x.Tentativas);
            });
        }

        // No dia seguinte a semana corrente (dividida no dia 09) ganha o dia 10 como item próprio.
        await using (var d = fixture.CriarDbContext()) await Criar(d).PlanejarAsync(hoje.AddDays(1), default);
        await using (var ver = fixture.CriarDbContext())
        {
            var semana = await ver.SisregIndicadorColetas.SingleAsync(c =>
                c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.Escopo == "" && c.JanelaInicio == corrente);
            Assert.Equal(new DateOnly(2031, 6, 10), semana.JanelaFim);
            Assert.Equal(StatusColetaIndicador.Falha, semana.Status);

            var diasDaCorrente = await ver.SisregIndicadorColetas
                .Where(c => c.Coletor == ColetorIndicadorSisreg.FaltasRecentes && c.Escopo == PlanoColetaIndicadores.EscopoDia
                            && c.JanelaInicio >= corrente)
                .Select(c => c.JanelaInicio).OrderBy(x => x).ToListAsync();
            Assert.Equal([corrente, new DateOnly(2031, 6, 10)], diasDaCorrente);
        }

        await LimparAsync(db);
    }

    /// <summary>
    /// Mês passado com o total declarado (a carga de 30/09) e sem as linhas: ganha uma AMOSTRA de motivos —
    /// uma vez só, e sem tocar na janela do total. Mês que já tem as linhas não ganha.
    /// </summary>
    [Fact]
    public async Task Mes_passado_com_total_e_sem_linhas_ganha_amostra_de_motivos()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);
        var semLinhas = new DateOnly(2030, 5, 1);
        var comLinhas = new DateOnly(2030, 6, 1);
        foreach (var m in new[] { semLinhas, comLinhas })
        {
            db.SisregIndicadorColetas.Add(new SisregIndicadorColeta
            {
                Id = Guid.NewGuid(), Coletor = ColetorIndicadorSisreg.Canceladas, JanelaInicio = m,
                JanelaFim = m.AddMonths(1).AddDays(-1), Status = StatusColetaIndicador.Concluida, Tentativas = 1,
                Linhas = 2, LidoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
            });
        }
        foreach (var i in new[] { 1, 2 })
        {
            db.SisregMarcacoesCanceladas.Add(new SisregMarcacaoCancelada
            {
                Id = Guid.NewGuid(), CodigoSolicitacao = $"99000070{i}",
                CanceladoEm = new DateTime(2030, 6, 10 + i, 15, 0, 0, DateTimeKind.Utc), LidoEm = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        await Criar(db).PlanejarAsync(new DateOnly(2031, 6, 10), default);
        await using (var db2 = fixture.CriarDbContext()) await Criar(db2).PlanejarAsync(new DateOnly(2031, 6, 10), default);

        await using var ver = fixture.CriarDbContext();
        var amostras = await ver.SisregIndicadorColetas
            .Where(c => c.Escopo == PlanoColetaIndicadores.EscopoAmostra && c.JanelaInicio >= InicioDoTeste).ToListAsync();
        Assert.Equal(semLinhas, Assert.Single(amostras).JanelaInicio);
        Assert.Equal(2, (await ver.SisregIndicadorColetas.SingleAsync(c =>
            c.Coletor == ColetorIndicadorSisreg.Canceladas && c.JanelaInicio == semLinhas && c.Escopo == "")).Linhas);

        await ver.SisregMarcacoesCanceladas.Where(c => c.CodigoSolicitacao.StartsWith("99000070")).ExecuteDeleteAsync();
        await LimparAsync(ver);
    }
}

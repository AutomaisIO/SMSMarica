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

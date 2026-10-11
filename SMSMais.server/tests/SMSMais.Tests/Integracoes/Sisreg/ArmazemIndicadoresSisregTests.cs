using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Integracoes.SisregWeb.Indicadores;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// As escritas do coletor dos Indicadores contra o Postgres: substituir a janela troca TODAS as linhas
/// dela (e só dela), e os upserts não duplicam. Datas em 2030 para não cruzar com outros testes.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ArmazemIndicadoresSisregTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Substituir_faltas_troca_so_a_janela()
    {
        await using var db = fixture.CriarDbContext();
        var armazem = new ArmazemIndicadoresSisreg(db);
        var ini = new DateOnly(2030, 2, 1);
        var fim = new DateOnly(2030, 2, 8);
        var fora = new FaltaLidaSisreg("990000009", null, new DateOnly(2030, 2, 9), null, null);
        await armazem.SubstituirFaltasAsync(fora.DataExecucao, fora.DataExecucao, [fora], default);

        await armazem.SubstituirFaltasAsync(ini, fim,
            [new("990000001", "U", ini, "08:00", "P"), new("990000002", "U", fim, "09:00", "P"), new("990000002", "U", fim, "09:00", "P")],
            default);
        Assert.Equal(2, await armazem.ContarFaltasAsync(ini, fim, default));

        await armazem.SubstituirFaltasAsync(ini, fim, [new("990000003", "U", ini, "10:00", "P")], default);

        await using var ver = fixture.CriarDbContext();
        var janela = await ver.SisregFaltasOficiais.Where(f => f.DataExecucao >= ini && f.DataExecucao <= fim).ToListAsync();
        Assert.Equal("990000003", Assert.Single(janela).CodigoSolicitacao);
        Assert.True(await ver.SisregFaltasOficiais.AnyAsync(f => f.CodigoSolicitacao == "990000009"), "fora da janela não é tocado");

        await ver.SisregFaltasOficiais.Where(f => f.DataExecucao >= ini && f.DataExecucao <= fora.DataExecucao).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Leitura do dia por unidade executante (11/10/2026): a unidade só troca as faltas DELA — as de
    /// agendamento nosso com aquela executante. A falta de outra unidade e a de agendamento que não temos
    /// (sem executante conhecida) ficam como a leitura da rede deixou.
    /// </summary>
    [Fact]
    public async Task Substituir_faltas_da_executante_troca_so_as_dela()
    {
        await using var db = fixture.CriarDbContext();
        var armazem = new ArmazemIndicadoresSisreg(db);
        var dia = new DateOnly(2030, 4, 10);
        var cnesA = $"A{Random.Shared.Next(100_000, 999_999)}";
        var cnesB = $"B{Random.Shared.Next(100_000, 999_999)}";
        var a = Unidade(db, cnesA);
        var b = Unidade(db, cnesB);
        var raiz = Random.Shared.NextInt64(100_000_000, 899_999_999);
        string Cod(int i) => (raiz + i).ToString();
        Agendamento(db, a, dia, Cod(1)); // A: faltou e segue na lista
        Agendamento(db, a, dia, Cod(2)); // A: a unidade trocou para chegada — sai
        Agendamento(db, a, dia, Cod(3)); // A: falta marcada depois — entra
        Agendamento(db, b, dia, Cod(4)); // B: não relida — fica
        await db.SaveChangesAsync();
        FaltaLidaSisreg F(int i) => new(Cod(i), "U", dia, "08:00", "P");
        await armazem.SubstituirFaltasAsync(dia, dia, [F(1), F(2), F(4), F(5)], default); // 5: agendamento que não temos

        Assert.Equal(2, await armazem.ContarFaltasDaExecutanteAsync(dia, cnesA, default));
        var total = await armazem.SubstituirFaltasDaExecutanteAsync(dia, cnesA, [F(1), F(3), F(3)], default);

        await using var ver = fixture.CriarDbContext();
        var codigos = await ver.SisregFaltasOficiais.Where(f => f.DataExecucao == dia)
            .Select(f => f.CodigoSolicitacao).OrderBy(c => c).ToListAsync();
        Assert.Equal([Cod(1), Cod(3), Cod(4), Cod(5)], codigos);
        Assert.Equal(4, total);
        Assert.Equal(2, await armazem.ContarFaltasDaExecutanteAsync(dia, cnesA, default));

        await ver.SisregFaltasOficiais.Where(f => f.DataExecucao == dia).ExecuteDeleteAsync();
        await ver.Solicitacoes.Where(s => s.UnidadeExecutanteId == a || s.UnidadeExecutanteId == b).ExecuteDeleteAsync();
        await ver.Unidades.Where(u => u.Id == a || u.Id == b).ExecuteDeleteAsync();
    }

    private static Guid Unidade(SmsMaisDbContext db, string cnes)
    {
        var u = new Unidade { Id = Guid.NewGuid(), Nome = $"EXECUTANTE {cnes}", Cnes = cnes, CriadoEm = DateTime.UtcNow };
        db.Unidades.Add(u);
        return u.Id;
    }

    private static void Agendamento(SmsMaisDbContext db, Guid unidade, DateOnly dia, string codigo) =>
        db.Solicitacoes.Add(new Solicitacao
        {
            Id = Guid.NewGuid(),
            PacienteId = Guid.NewGuid(),
            Categoria = CategoriaSolicitacao.Consulta,
            UnidadeExecutanteId = unidade,
            SolicitanteNome = "DR TESTE",
            ProcedimentoTexto = "CONSULTA DE TESTE",
            Status = StatusSolicitacao.Agendada,
            StatusConfirmacao = StatusConfirmacaoAgendamento.Pendente,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            DataAgendada = dia.ToDateTime(new TimeOnly(13, 0), DateTimeKind.Utc),
            DataSolicitacao = dia.AddDays(-30),
            CodigoSolicitacao = codigo,
            RawSisreg = "x",
            CriadoEm = DateTime.UtcNow,
        });

    [Fact]
    public async Task Substituir_ppi_troca_a_competencia_e_upserts_nao_duplicam()
    {
        await using var db = fixture.CriarDbContext();
        var armazem = new ArmazemIndicadoresSisreg(db);
        var comp = new DateOnly(2030, 3, 1);

        await armazem.SubstituirPpiAsync(comp, [new("1", "A1", "X", 10, 1, 9, "FISICO"), new("2", "A2", "Y", 5, 5, 0, "FISICO")], default);
        await armazem.SubstituirPpiAsync(comp, [new("1", "A1", "X", 10, 3, 7, "FISICO")], default);
        Assert.Equal(1, await armazem.ContarPpiAsync(comp, default));

        var desfecho = new DesfechoLidoSisreg("990000011", new DateOnly(2030, 3, 2), "CONSULTA", "SOL/DEV/REG");
        Assert.Equal(1, await armazem.GravarDesfechosAsync("1234567", [(desfecho, SituacaoDesfechoSisreg.Devolvida)], default));
        Assert.Equal(0, await armazem.GravarDesfechosAsync("1234567", [(desfecho, SituacaoDesfechoSisreg.Devolvida)], default));
        Assert.Equal(1, await armazem.GravarDesfechosAsync("1234567", [(desfecho, SituacaoDesfechoSisreg.Negada)], default));

        await using var ver = fixture.CriarDbContext();
        var gravados = await ver.SisregSolicitacaoDesfechos.Where(d => d.CodigoSolicitacao == "990000011").ToListAsync();
        Assert.Equal(2, gravados.Count);
        Assert.All(gravados, d => Assert.Equal("1234567", d.UnidadeSolicitanteCnes));
        Assert.All(gravados, d => Assert.Equal(OrigemDesfechoSisreg.Tela, d.Origem));

        await ver.SisregSolicitacaoDesfechos.Where(d => d.CodigoSolicitacao == "990000011").ExecuteDeleteAsync();
        await ver.SisregPpiCotas.Where(c => c.Competencia == comp).ExecuteDeleteAsync();
    }
}

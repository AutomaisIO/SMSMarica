using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Integracoes.SisregWeb.Indicadores;
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

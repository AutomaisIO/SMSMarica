using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SisregWeb.Estatisticas;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Estatísticas dos operadores da regulação (15/09/2026). O login vem dentro da linha crua do export
/// (coluna 33) e a conta é SQL de verdade — por isso contra banco.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class EstatisticasOperadoresTests(PostgresFixture fixture)
{
    private sealed record Cenario(
        DateOnly De, DateOnly Ate, Usuario Pessoa, string LoginA, string LoginB, string LoginC, string LoginD);

    private static string Raw(string opSol, string opAut, string valor)
    {
        var c = new string[38];
        Array.Fill(c, string.Empty);
        c[0] = "1";
        c[30] = opSol;
        c[32] = opAut;
        c[33] = valor;
        return string.Join(';', c);
    }

    private static Solicitacao Autorizacao(
        Guid unidade, Guid solicitante, string opAut, DateOnly dia, string opSol = "UNIDADE-QUALQUER") =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CodigoSolicitacao = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(),
            UnidadeExecutanteId = unidade,
            UnidadeSolicitanteId = solicitante,
            ProcedimentoCodigoSisreg = "0205001",
            ProcedimentoTexto = "PROCEDIMENTO DE TESTE",
            RawSisreg = Raw(opSol, opAut, "10.50"),
            DataSolicitacao = dia.AddDays(-4),
            DataRegulacao = dia,
            DataAgendada = dia.AddDays(10).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
            Status = StatusSolicitacao.Agendada,
            CriadoEm = DateTime.UtcNow,
        };

    /// <summary>
    /// Período de 30 dias num ano distante e logins únicos por execução: a bancada é compartilhada e
    /// os testes deixam linhas para trás.
    /// </summary>
    private async Task<Cenario> MontarAsync(SmsMaisDbContext db)
    {
        var n = Random.Shared.Next(100_000, 999_999);
        var de = new DateOnly(2150 + Random.Shared.Next(0, 40), 3, 1);
        var ate = de.AddDays(29);
        string a = $"EST{n}-A", b = $"EST{n}-B", c = $"EST{n}-C", d = $"EST{n}-D";

        var executante = new Unidade { Id = Guid.NewGuid(), Nome = $"EXECUTANTE EST {n}", CriadoEm = DateTime.UtcNow };
        var solicitante = new Unidade { Id = Guid.NewGuid(), Nome = $"SOLICITANTE EST {n}", CriadoEm = DateTime.UtcNow };
        var pessoa = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = $"REGULADORA TESTE {n}",
            SenhaHash = "x",
            LoginsSisreg = [a, b],
            CriadoEm = DateTime.UtcNow,
        };
        db.AddRange(executante, solicitante, pessoa);

        Solicitacao S(string op, int dia, string? opSol = null) =>
            Autorizacao(executante.Id, solicitante.Id, op, de.AddDays(dia), opSol ?? "UNIDADE-QUALQUER");

        // Pessoa (logins A e B): 3 no dia 0, 2+1 no dia 1, 1 no dia 2 → 7 em 3 dias, pico 3.
        db.Solicitacoes.AddRange(S(a, 0), S(a, 0), S(a, 0), S(a, 1), S(a, 1), S(b, 1), S(b, 2));
        // C sem pessoa: 4 no dia 0, uma delas pedida pelo próprio login.
        db.Solicitacoes.AddRange(S(c, 0), S(c, 0), S(c, 0), S(c, 0, opSol: c));
        // D não habilitado: só conta no total geral.
        db.Solicitacoes.AddRange(S(d, 0), S(d, 0), S(d, 0), S(d, 0), S(d, 0));
        // Período anterior (mesmo tamanho, logo antes): 2 da pessoa.
        db.Solicitacoes.AddRange(S(a, -5), S(a, -5));

        await db.SaveChangesAsync();
        return new Cenario(de, ate, pessoa, a, b, c, d);
    }

    private static EstatisticasOperadoresService Servico(SmsMaisDbContext db) =>
        new(db, null!, new MemoryCache(new MemoryCacheOptions()));

    private static HashSet<string> Habilitados(params string[] logins) => new(logins, StringComparer.Ordinal);

    [Fact]
    public async Task Equipe_soma_logins_da_mesma_pessoa_e_separa_dia_trabalhado_de_dia_corrido()
    {
        await using var db = fixture.CriarDbContext();
        var c = await MontarAsync(db);

        var r = await Servico(db).EquipeAsync(c.De, c.Ate, Habilitados(c.LoginA, c.LoginB, c.LoginC));

        Assert.Equal(30, r.DiasCorridos);
        Assert.Equal(2, r.Ranking.Count);

        var pessoa = r.Ranking[0];
        Assert.Equal($"u:{c.Pessoa.Id}", pessoa.Chave);
        Assert.Equal(c.Pessoa.NomeCompleto, pessoa.Nome);
        Assert.Equal([c.LoginA, c.LoginB], pessoa.Logins);
        Assert.Equal(7, pessoa.Autorizacoes);
        Assert.Equal(3, pessoa.DiasTrabalhados);
        Assert.Equal(2.3, pessoa.MediaDiaTrabalhado);
        Assert.Equal(0.2, pessoa.MediaDiaCorrido);
        Assert.Equal(3, pessoa.PicoDiario);
        Assert.Equal(c.De, pessoa.DiaDoPico);           // empate entre dia 0 e dia 1: o primeiro
        Assert.Equal(4, pessoa.EsperaMedianaDias);
        Assert.Equal(10, pessoa.AntecedenciaMedianaDias);
        Assert.Equal(73.5m, pessoa.ValorRegulado);

        var semPessoa = r.Ranking[1];
        Assert.Equal($"l:{c.LoginC}", semPessoa.Chave);
        Assert.Equal(c.LoginC, semPessoa.Nome);
        Assert.Equal(25.0, semPessoa.PercentualProprioPedido);

        Assert.Equal(11, r.Atual.Autorizacoes);
        Assert.Equal(2, r.Atual.Operadores);
        Assert.Equal(2, r.Anterior.Autorizacoes);
        Assert.True(r.AutorizacoesTodosOsLogins >= 16);  // D entra só aqui
        Assert.Equal(100.0, r.ConcentracaoTop3Percentual);   // só 2 pessoas: as "3 maiores" são todas

        var dia0 = Assert.Single(r.PorDia, x => x.Dia == c.De);
        Assert.Equal(7, dia0.Autorizacoes);
        Assert.Equal(2, dia0.Operadores);
        Assert.Equal(7, r.PorDiaSemana.Count);
        Assert.Contains(r.TopUnidadesExecutantes, t => t.Rotulo.StartsWith("EXECUTANTE EST") && t.Autorizacoes == 11);
    }

    [Fact]
    public async Task Sem_ninguem_habilitado_nao_ha_ranking()
    {
        await using var db = fixture.CriarDbContext();
        var c = await MontarAsync(db);

        var r = await Servico(db).EquipeAsync(c.De, c.Ate, Habilitados());

        Assert.Empty(r.Ranking);
        Assert.Equal(0, r.Atual.Autorizacoes);
        Assert.Equal(0, r.Habilitados);
    }

    [Fact]
    public async Task Individual_bate_com_a_linha_do_ranking_e_recusa_quem_nao_esta_habilitado()
    {
        await using var db = fixture.CriarDbContext();
        var c = await MontarAsync(db);
        var habilitados = Habilitados(c.LoginA, c.LoginB, c.LoginC);
        var servico = Servico(db);

        var r = await servico.IndividualAsync($"u:{c.Pessoa.Id}", c.De, c.Ate, habilitados);

        Assert.Equal(7, r.Metricas!.Autorizacoes);
        Assert.Equal(3, r.PorDia.Count);
        Assert.Equal(2, r.Anterior.Autorizacoes);
        Assert.Equal([c.LoginA, c.LoginB], r.Logins);

        await Assert.ThrowsAsync<NaoEncontradoException>(() =>
            servico.IndividualAsync($"l:{c.LoginD}", c.De, c.Ate, habilitados));
    }

    [Fact]
    public async Task Periodo_maior_que_um_ano_e_recusado()
    {
        await using var db = fixture.CriarDbContext();

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            Servico(db).EquipeAsync(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2), Habilitados()));
    }

    [Fact]
    public void Configuracao_normaliza_os_logins_gravados()
    {
        var json = new JsonObject
        {
            [OperadoresDaEstatistica.Chave] = new JsonArray(" emilia-regulador ", "EMILIA-REGULADOR", "", "Claudia"),
        };

        var lidos = OperadoresDaEstatistica.Ler(json);

        Assert.Equal(["CLAUDIA", "EMILIA-REGULADOR"], lidos.Order());
    }
}

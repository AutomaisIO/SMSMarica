using Npgsql;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Inteligencia.Fontes;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Inteligencia;

/// <summary>
/// Fonte Postgres da Consulta Inteligente (bases Regulação e Atendimento, 12/09/2026): o banco é o
/// de produção do próprio SMSMais, então "só lê" precisa valer no banco, não só no guard.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PostgresFonteTests(PostgresFixture fixture)
{
    private PostgresFonte CriarFonte(int maxLinhas = 1000)
    {
        var cs = new NpgsqlConnectionStringBuilder(fixture.ConnectionString);
        return new PostgresFonte(cs.Host!, cs.Port, cs.Database!, cs.Username!, cs.Password ?? string.Empty,
            commandTimeoutSegundos: 30, maxLinhas: maxLinhas);
    }

    [Fact]
    public async Task Le_e_capa_as_linhas()
    {
        var r = await CriarFonte(maxLinhas: 10).ExecutarAsync("SELECT n FROM generate_series(1, 50) AS n");

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(["n"], r.Colunas);
        Assert.Equal(10, r.Linhas.Count);
    }

    /// <summary>A consulta roda numa transação só-leitura: função que escreveria falha no banco.</summary>
    [Fact]
    public async Task A_consulta_roda_em_transacao_somente_leitura()
    {
        var r = await CriarFonte().ExecutarAsync("SELECT current_setting('transaction_read_only') AS ro");

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("on", r.Linhas[0][0]);
    }

    [Fact]
    public async Task Escrita_e_barrada_antes_de_chegar_ao_banco()
    {
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            CriarFonte().ExecutarAsync("DELETE FROM smsmarica.unidade"));
    }

    /// <summary>SQL errado não derruba nada: volta como erro legível para a IA ajustar.</summary>
    [Fact]
    public async Task Erro_do_banco_volta_como_resultado()
    {
        var r = await CriarFonte().ExecutarAsync("SELECT coluna_que_nao_existe FROM smsmarica.unidade");

        Assert.False(r.Sucesso);
        Assert.Contains("coluna_que_nao_existe", r.Erro);
    }

    [Fact]
    public async Task Testa_a_conexao()
    {
        Assert.True(await CriarFonte().TestarConexaoAsync());
    }
}

/// <summary>Quem pode usar cada base, sem banco.</summary>
public class PermissaoDaFonteTests
{
    [Theory]
    [InlineData(TipoFonte.Atendimento, ModuloPermissao.InteligenciaAtendimento)]
    [InlineData(TipoFonte.Regulacao, null)]
    [InlineData(TipoFonte.Salux, null)]
    public void Modulo_exigido_por_tipo(TipoFonte tipo, ModuloPermissao? esperado) =>
        Assert.Equal(esperado, PermissaoDaFonte.ModuloExigido(tipo));

    [Theory]
    [InlineData(TipoFonte.Regulacao, true)]
    [InlineData(TipoFonte.Atendimento, true)]
    [InlineData(TipoFonte.Salux, false)]
    [InlineData(TipoFonte.SqlServer, false)]
    public void Auditoria_obrigatoria_so_nas_bases_do_smsmais(TipoFonte tipo, bool esperado) =>
        Assert.Equal(esperado, PermissaoDaFonte.AuditoriaObrigatoria(tipo));

    /// <summary>Fail-closed: sem operador identificado, a base Atendimento não abre.</summary>
    [Fact]
    public async Task Atendimento_sem_usuario_nao_passa()
    {
        Assert.False(await PermissaoDaFonte.PodeUsarAsync(null!, null, TipoFonte.Atendimento, default));
    }

    [Fact]
    public async Task Base_sem_modulo_proprio_nao_consulta_permissao()
    {
        Assert.True(await PermissaoDaFonte.PodeUsarAsync(null!, null, TipoFonte.Regulacao, default));
    }
}

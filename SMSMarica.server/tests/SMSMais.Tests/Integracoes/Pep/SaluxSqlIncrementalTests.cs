using SMSMarica.Core.Integracoes.Pep.Estrategias.Salux;
using SMSMarica.Core.Integracoes.Pep.Leitura;

namespace SMSMais.Tests.Integracoes.Pep;

/// <summary>
/// Trava as correções do incremental do ADR-0024 no SQL gerado: paciente novo entra pelo
/// OR dt_cadastro (D4), o literal de data é convertido UTC→BRT (D5), a marca e o filtro do
/// eDoc falam da MESMA coluna (D6) e o incremental é dirigido pela origem (D3).
/// </summary>
public class SaluxSqlIncrementalTests
{
    // 12:00 UTC = 09:00 em Brasília (−03:00, sem horário de verão desde 2019).
    private static readonly DateTime MeioDiaUtc = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Literal_oracle_converte_utc_para_brt()
        => Assert.Equal("TO_DATE('2026-07-27 09:00:00','YYYY-MM-DD HH24:MI:SS')", SaluxTempo.LiteralOracle(MeioDiaUtc));

    [Fact]
    public void Literal_oracle_na_borda_da_meia_noite_volta_um_dia()
        // 01:30 UTC de 27/07 ainda é 22:30 BRT de 26/07 — o ToLocalTime() antigo (no-op em
        // droplet UTC) filtraria 3h de dados a mais ou a menos aqui.
        => Assert.Equal("TO_DATE('2026-07-26 22:30:00','YYYY-MM-DD HH24:MI:SS')",
            SaluxTempo.LiteralOracle(new DateTime(2026, 7, 27, 1, 30, 0, DateTimeKind.Utc)));

    [Fact]
    public void Parse_utc_faz_o_caminho_inverso_do_literal()
        => Assert.Equal(MeioDiaUtc, SaluxTempo.ParseUtc("2026-07-27T09:00:00"));

    [Fact]
    public void Incremental_de_pacientes_cobre_cadastro_novo_e_edicao()
    {
        var sql = SaluxImportacaoStrategy.SqlPacientes(null, 300, MeioDiaUtc, null);
        Assert.Contains("dt_alteracao > TO_DATE('2026-07-27 09:00:00'", sql);
        Assert.Contains("OR dt_cadastro > TO_DATE('2026-07-27 09:00:00'", sql);
        // Timestamps da origem no SELECT — alimentam a marca (máximo da ORIGEM, nunca "agora").
        Assert.Contains("AS dt_cadastro", sql);
        Assert.Contains("AS dt_alteracao", sql);
    }

    [Fact]
    public void Baas_pegam_novos_E_editados_na_janela()
    {
        var sql = SaluxImportacaoStrategy.SqlBaas([1, 2], MeioDiaUtc);
        Assert.Contains("b.dt_atendimento > TO_DATE('2026-07-27 09:00:00'", sql);
        // BAA EDITADO reaparece: dt_atualizacao (100% preenchida) ancorada na janela indexada.
        Assert.Contains("b.dt_atendimento > SYSDATE - 120 AND b.dt_atualizacao > TO_DATE('2026-07-27 09:00:00'", sql);
    }

    [Fact]
    public void Edoc_log_e_pollado_por_pk_sequencial()
    {
        var sql = SaluxImportacaoStrategy.SqlEdocLog(123456, 5000);
        Assert.Contains("log.id_edoc_movimento_log > 123456", sql);
        Assert.Contains("ORDER BY log.id_edoc_movimento_log", sql);
        Assert.Contains("FETCH NEXT 5000 ROWS ONLY", sql);
    }

    [Fact]
    public void Edocs_selecionam_e_filtram_pela_mesma_coluna()
    {
        var sql = SaluxImportacaoStrategy.SqlEdocs([1], MeioDiaUtc);
        Assert.Contains("mov.dt_inclusao > TO_DATE('2026-07-27 09:00:00'", sql);
        Assert.Contains("AS dt_incl", sql); // a marca avança por dt_inclusao, não por dt_episodio
    }

    [Fact]
    public void Cds_com_atendimento_novo_unem_baa_edoc_e_fia()
    {
        var sql = SaluxImportacaoStrategy.SqlCdsComAtendimentoNovo(MeioDiaUtc, MeioDiaUtc.AddHours(-2), MeioDiaUtc.AddHours(-1));
        Assert.Contains("FROM infosaude.baa b WHERE (b.dt_atendimento > TO_DATE('2026-07-27 09:00:00'", sql);
        Assert.Contains("b.dt_atualizacao > TO_DATE('2026-07-27 09:00:00'", sql);
        Assert.Contains("UNION", sql);
        Assert.Contains("mov.dt_inclusao > TO_DATE('2026-07-27 07:00:00'", sql);
        Assert.Contains("mov.nr_baa IS NOT NULL", sql);
        // FIA: novas/altas desde a marca + em curso SEMPRE (re-lida a cada ciclo).
        Assert.Contains("f.dt_baixa > TO_DATE('2026-07-27 08:00:00'", sql);
        Assert.Contains("f.dt_alta > TO_DATE('2026-07-27 08:00:00'", sql);
        Assert.Contains("(f.dt_alta IS NULL AND f.dt_baixa >= SYSDATE - 120)", sql);
        Assert.Contains("NVL(f.cd_paciente_unificado, f.cd_paciente)", sql);
    }

    [Fact]
    public void Cds_sem_marca_de_edoc_nao_filtram_por_data()
    {
        var sql = SaluxImportacaoStrategy.SqlCdsComAtendimentoNovo(MeioDiaUtc, null, null);
        Assert.Contains("WHERE mov.nr_baa IS NOT NULL", sql);
        Assert.DoesNotContain("mov.dt_inclusao >", sql);
        // FIA sem marca fica na janela de 120 dias — o histórico vem pelo modo Completo.
        Assert.Contains("f.dt_baixa > SYSDATE - 120", sql);
    }
}

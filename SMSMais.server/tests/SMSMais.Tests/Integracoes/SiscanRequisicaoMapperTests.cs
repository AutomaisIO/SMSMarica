using FluentAssertions;
using SMSMais.Core.Integracoes.SiscanWeb.Requisicao;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// O de-para entre a nossa anamnese e a requisição do SISCAN.
///
/// <para>Cada caso aqui é uma afirmação sobre a saúde de uma pessoa gravada numa base federal de
/// rastreamento de câncer. Por isso o que está fixado não é "o código faz X": é <b>não inventar</b>
/// — "Não sabe" onde não perguntamos, lacuna onde o SISCAN exige Sim ou Não.</para>
/// </summary>
public class SiscanRequisicaoMapperTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 22);
    private const string Prontuario = "260827047";

    private static string Json(string corpoSiscan = "{}", string historico = "{}",
        string queixas = "{}", string risco = "{}") =>
        $$"""
        {
          "historicoClinico": {{historico}},
          "queixas": {{queixas}},
          "avaliacaoRisco": {{risco}},
          "siscan": {{corpoSiscan}}
        }
        """;

    private static Dictionary<string, List<string>> Agrupar(CamposRequisicao campos) =>
        campos.Campos
            .GroupBy(c => c.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Value).ToList(), StringComparer.Ordinal);

    // ------------------------------------------------------------------ idade

    [Theory]
    [InlineData(1965, 6, 1, "02")]    // 61 anos — rastreamento
    [InlineData(1990, 9, 21, "02")]   // fez 36 ontem — rastreamento
    [InlineData(1990, 9, 22, "02")]   // faz 36 hoje — o corte é >= 36
    [InlineData(1990, 9, 23, "01")]   // faz 36 amanhã — ainda diagnóstica
    [InlineData(2000, 1, 1, "01")]    // 26 anos — diagnóstica
    public void Tipo_de_mamografia_sai_da_idade(int ano, int mes, int dia, string esperado)
    {
        SiscanRequisicaoMapper.TipoMamografiaPorIdade(new DateOnly(ano, mes, dia), Hoje)
            .Should().Be(esperado);
    }

    // ------------------------------------------------------------------ não inventar

    /// <summary>
    /// A v1 não perguntava "mamas já examinadas" nem "radioterapia". O SISCAN oferece "Não Sabe",
    /// que é a verdade sobre o que sabemos — e não um buraco tapado com "Não".
    /// </summary>
    [Fact]
    public void Anamnese_antiga_responde_nao_sabe_onde_nao_perguntamos()
    {
        var conteudo = Json(historico: """{"jaRealizouCirurgiaMamaria":{"resposta":false}}""");

        var campos = Agrupar(SiscanRequisicaoMapper.Montar(conteudo, Prontuario, Hoje, "02"));

        campos[SiscanRequisicaoMapper.CampoMamasExaminadas].Should().Equal("03");
        campos[SiscanRequisicaoMapper.CampoRadioterapia].Should().Equal("03");
    }

    /// <summary>
    /// "Fez cirurgia de mama?" não tem "Não Sabe" no SISCAN. Sem resposta na anamnese, isto vira
    /// LACUNA — mandar "Não" seria afirmar o que ninguém apurou.
    /// </summary>
    [Fact]
    public void Cirurgia_sem_resposta_vira_lacuna_e_nao_um_nao()
    {
        var campos = SiscanRequisicaoMapper.Montar(Json(), Prontuario, Hoje, "02");

        campos.Lacunas.Should().ContainSingle()
            .Which.Campo.Should().Be("historicoClinico.jaRealizouCirurgiaMamaria");
        campos.Campos.Should().NotContain(c => c.Key == SiscanRequisicaoMapper.CampoCirurgia);
    }

    // ------------------------------------------------------------------ nódulo

    /// <summary>
    /// Nódulo nas duas mamas vai com a MESMA chave repetida. Um dicionário perderia a segunda —
    /// e o SISCAN registraria só uma mama, sem erro nenhum.
    /// </summary>
    [Fact]
    public void Nodulo_nas_duas_mamas_vai_com_a_chave_repetida()
    {
        var queixas = """{"sintomas":{"noduloPalpavel":{"direita":true,"esquerda":true}}}""";

        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(queixas: queixas, historico: """{"jaRealizouCirurgiaMamaria":{"resposta":false}}"""),
            Prontuario, Hoje, "02"));

        campos[SiscanRequisicaoMapper.CampoNodulo].Should().BeEquivalentTo("01", "02");
    }

    [Fact]
    public void Sem_nodulo_marcado_vai_nao()
    {
        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(historico: """{"jaRealizouCirurgiaMamaria":{"resposta":false}}"""),
            Prontuario, Hoje, "02"));

        campos[SiscanRequisicaoMapper.CampoNodulo].Should().Equal("04");
    }

    // ------------------------------------------------------------------ risco

    /// <summary>Decisão do Bernardo (22/09/2026): acima de Baixo entra como risco elevado.</summary>
    [Theory]
    [InlineData("Alto", "01")]
    [InlineData("Moderado", "01")]
    [InlineData("Baixo", "02")]
    public void Classificacao_de_risco_vira_sim_ou_nao(string classificacao, string esperado)
    {
        var risco = $$"""{"classificacao":"{{classificacao}}"}""";

        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(risco: risco, historico: """{"jaRealizouCirurgiaMamaria":{"resposta":false}}"""),
            Prontuario, Hoje, "02"));

        campos[SiscanRequisicaoMapper.CampoRiscoElevado].Should().Equal(esperado);
    }

    /// <summary>
    /// Sem classificação preenchida (é o caso real de 02/09/2026), valem os quatro critérios:
    /// todos falsos = Não; qualquer verdadeiro = Sim; nenhum respondido = Não Sabe.
    /// </summary>
    [Theory]
    [InlineData("""{"familiar1GrauCancerMama":false,"cancerMamaAntes50Familia":false,"historicoPessoalCancer":false,"mutacaoGeneticaConhecida":false}""", "02")]
    [InlineData("""{"familiar1GrauCancerMama":true,"cancerMamaAntes50Familia":false,"historicoPessoalCancer":false,"mutacaoGeneticaConhecida":false}""", "01")]
    [InlineData("{}", "03")]
    public void Sem_classificacao_valem_os_criterios(string risco, string esperado)
    {
        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(risco: risco, historico: """{"jaRealizouCirurgiaMamaria":{"resposta":false}}"""),
            Prontuario, Hoje, "02"));

        campos[SiscanRequisicaoMapper.CampoRiscoElevado].Should().Equal(esperado);
    }

    // ------------------------------------------------------------------ condicionais

    /// <summary>
    /// Radioterapia: três níveis. E o código da localização é contraintuitivo —
    /// <b>01 é Esquerda, 02 é Direita</b>. Trocar os dois grava o lado errado, sem aviso.
    /// </summary>
    [Fact]
    public void Radioterapia_leva_lado_e_ano_com_o_codigo_medido()
    {
        var siscan = """
            {"radioterapia":{"resposta":"sim","lado":"ambas","anoDireita":"2019","anoEsquerda":"2020"}}
            """;

        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(corpoSiscan: siscan, historico: """{"jaRealizouCirurgiaMamaria":{"resposta":false}}"""),
            Prontuario, Hoje, "02"));

        campos[SiscanRequisicaoMapper.CampoRadioterapia].Should().Equal("01");
        campos[SiscanRequisicaoMapper.CampoLocalRadioterapia].Should().Equal("03");
        campos["frm:anoRadioterapiaDireita"].Should().Equal("2019");
        campos["frm:anoRadioterapiaEsquerda"].Should().Equal("2020");
    }

    [Fact]
    public void Radioterapia_so_esquerda_nao_manda_o_ano_da_direita()
    {
        var siscan = """
            {"radioterapia":{"resposta":"sim","lado":"esquerda","anoDireita":"","anoEsquerda":"2021"}}
            """;

        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(corpoSiscan: siscan, historico: """{"jaRealizouCirurgiaMamaria":{"resposta":false}}"""),
            Prontuario, Hoje, "02"));

        campos[SiscanRequisicaoMapper.CampoLocalRadioterapia].Should().Equal("01");
        campos.Should().NotContainKey("frm:anoRadioterapiaDireita");
        campos["frm:anoRadioterapiaEsquerda"].Should().Equal("2021");
    }

    /// <summary>A chave do nosso questionário espelha o nome do campo deles, de propósito.</summary>
    [Fact]
    public void Cirurgias_viram_o_campo_de_ano_por_tipo_e_lado()
    {
        var historico = """{"jaRealizouCirurgiaMamaria":{"resposta":true}}""";
        var siscan = """
            {"cirurgias":[
              {"tipo":"mastectomiaPoupadoraPele","lado":"direita","ano":"2018"},
              {"tipo":"inclusaoImplantes","lado":"esquerda","ano":"2019"}
            ]}
            """;

        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(corpoSiscan: siscan, historico: historico), Prontuario, Hoje, "02"));

        campos[SiscanRequisicaoMapper.CampoCirurgia].Should().Equal("S");
        campos["frm:anoMastectomiaPoupadoraPeleDireita"].Should().Equal("2018");
        campos["frm:anoInclusaoImplantesEsquerda"].Should().Equal("2019");
    }

    /// <summary>Prótese mamária é "inclusão de implantes" para o SISCAN: quem tem prótese fez cirurgia.</summary>
    [Fact]
    public void Protese_conta_como_cirurgia_de_mama()
    {
        var historico = """
            {"jaRealizouCirurgiaMamaria":{"resposta":false},"possuiProteseMamaria":{"resposta":true}}
            """;

        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(historico: historico), Prontuario, Hoje, "02"));

        campos[SiscanRequisicaoMapper.CampoCirurgia].Should().Equal("S");
    }

    // ------------------------------------------------------------------ rastreamento

    [Theory]
    [InlineData("""{"historicoPessoalCancer":true}""", "03")]
    [InlineData("""{"familiar1GrauCancerMama":true}""", "02")]
    [InlineData("""{"cancerMamaAntes50Familia":true}""", "02")]
    [InlineData("""{"familiar1GrauCancerMama":false}""", "01")]
    public void Populacao_do_rastreamento_sai_dos_criterios_de_risco(string risco, string esperado)
    {
        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(risco: risco, historico: """{"jaRealizouCirurgiaMamaria":{"resposta":false}}"""),
            Prontuario, Hoje, "02"));

        campos[SiscanRequisicaoMapper.CampoPopulacaoRastreamento].Should().Equal(esperado);
    }

    /// <summary>Na diagnóstica não existe população-alvo — mandar o campo seria ruído.</summary>
    [Fact]
    public void Diagnostica_nao_leva_populacao()
    {
        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(historico: """{"jaRealizouCirurgiaMamaria":{"resposta":false}}"""),
            Prontuario, Hoje, "01"));

        campos.Should().NotContainKey(SiscanRequisicaoMapper.CampoPopulacaoRastreamento);
    }

    // ------------------------------------------------------------------ o resto

    [Fact]
    public void Prontuario_e_data_vao_no_formato_que_a_tela_espera()
    {
        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(historico: """{"jaRealizouCirurgiaMamaria":{"resposta":false}}"""),
            Prontuario, new DateOnly(2026, 6, 24), "02"));

        campos[SiscanRequisicaoMapper.CampoProntuario].Should().Equal(Prontuario);
        campos[SiscanRequisicaoMapper.CampoDataSolicitacao].Should().Equal("24/06/2026");
    }

    [Fact]
    public void Ano_da_ultima_mamografia_so_vai_se_ela_fez()
    {
        var historico = """
            {"jaRealizouMamografia":{"resposta":false},"jaRealizouCirurgiaMamaria":{"resposta":false}}
            """;

        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(corpoSiscan: """{"anoUltimaMamografia":"2020"}""", historico: historico),
            Prontuario, Hoje, "02"));

        campos[SiscanRequisicaoMapper.CampoFezMamografia].Should().Equal("02");
        campos.Should().NotContainKey(SiscanRequisicaoMapper.CampoAnoUltimaMamografia);
    }

    /// <summary>JSON quebrado não pode derrubar a geração com stack trace — vira "não sabe".</summary>
    [Fact]
    public void Conteudo_invalido_nao_explode()
    {
        var campos = SiscanRequisicaoMapper.Montar("{ isso não é json", Prontuario, Hoje, "02");

        campos.Campos.Should().NotBeEmpty();
        campos.Lacunas.Should().ContainSingle();
    }
}

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

    // ------------------------------------------------------------ data do exame

    /// <summary>
    /// O campo que o SISCAN chama de "Data da Solicitação" leva a data em que o exame foi FEITO,
    /// não a da ficha do SISREG (Bernardo, 23/09/2026). No caso real 260903032 a ficha era de
    /// 23/07 e o exame aconteceu em 23/09 — dois meses de diferença.
    /// </summary>
    [Fact]
    public void Data_do_exame_vem_do_dicom_quando_existe()
    {
        var (data, origem) = SiscanRequisicaoService.DataDoExameDe(
            dataEstudo: new DateTime(2026, 9, 23, 8, 26, 20),
            anamnesePreenchidaEm: new DateTime(2026, 9, 24, 11, 0, 0, DateTimeKind.Utc),
            realizadoEm: new DateTime(2026, 9, 23, 12, 3, 59, DateTimeKind.Utc),
            agoraUtc: new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc));

        data.Should().Be(new DateOnly(2026, 9, 23));
        origem.Should().Be(SiscanRequisicaoService.OrigemDataDoExame.Dicom);
    }

    /// <summary>
    /// Sem DICOM, vale o preenchimento da anamnese — ela é feita com a paciente na frente, no dia
    /// do exame. Medido em 23/09/2026: das 872 anamneses, 818 no MESMO dia do StudyDate e só 2 em
    /// dia diferente. E ela existe nos 52 casos em que o DICOM não existe.
    /// </summary>
    [Fact]
    public void Sem_dicom_vale_o_preenchimento_da_anamnese()
    {
        var (data, origem) = SiscanRequisicaoService.DataDoExameDe(
            dataEstudo: null,
            anamnesePreenchidaEm: new DateTime(2026, 9, 23, 11, 0, 0, DateTimeKind.Utc),
            realizadoEm: new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
            agoraUtc: new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc));

        data.Should().Be(new DateOnly(2026, 9, 23));
        origem.Should().Be(SiscanRequisicaoService.OrigemDataDoExame.PreenchimentoDaAnamnese);
    }

    /// <summary>
    /// `criado_em` da anamnese é instante UTC: precisa passar por Brasília. Uma anamnese salva às
    /// 00:30 UTC foi preenchida às 21:30 do DIA ANTERIOR aqui — sem a conversão, o exame entraria
    /// no SISCAN com a data de amanhã.
    /// </summary>
    [Fact]
    public void Anamnese_salva_de_madrugada_utc_conta_como_o_dia_anterior_aqui()
    {
        var (data, _) = SiscanRequisicaoService.DataDoExameDe(
            dataEstudo: null,
            anamnesePreenchidaEm: new DateTime(2026, 9, 24, 0, 30, 0, DateTimeKind.Utc),
            realizadoEm: null,
            agoraUtc: new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc));

        data.Should().Be(new DateOnly(2026, 9, 23));
    }

    /// <summary>
    /// <b>Anamnese depois do exame nunca acontece</b> (regra do Bernardo, 23/09/2026, e os 818 de
    /// 818 concordam). Um estudo com data POSTERIOR à anamnese não é um exame que demorou: é
    /// problema de conciliação — o estudo foi associado ao pedido errado. Ali o DICOM perde a
    /// confiança e vale a anamnese.
    ///
    /// <para>É o caso real 260824012: anamnese 25/08, estudo associado 22/09.</para>
    /// </summary>
    [Fact]
    public void Estudo_posterior_a_anamnese_e_conciliacao_errada_entao_vale_a_anamnese()
    {
        var (data, origem) = SiscanRequisicaoService.DataDoExameDe(
            dataEstudo: new DateTime(2026, 9, 22, 10, 0, 0),
            anamnesePreenchidaEm: new DateTime(2026, 8, 25, 14, 0, 0, DateTimeKind.Utc),
            realizadoEm: null,
            agoraUtc: new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc));

        data.Should().Be(new DateOnly(2026, 8, 25));
        origem.Should().Be(SiscanRequisicaoService.OrigemDataDoExame.AnamnesePorqueDicomEhPosterior);
    }

    /// <summary>
    /// O contrário é normal e o DICOM manda: anamnese digitada muito depois do exame acontece
    /// (registro retroativo). É o caso real 260701056 — exame 02/06, anamnese 01/07.
    /// </summary>
    [Fact]
    public void Anamnese_muito_depois_do_exame_e_normal_e_vale_o_dicom()
    {
        var (data, origem) = SiscanRequisicaoService.DataDoExameDe(
            dataEstudo: new DateTime(2026, 6, 2, 9, 0, 0),
            anamnesePreenchidaEm: new DateTime(2026, 7, 1, 14, 0, 0, DateTimeKind.Utc),
            realizadoEm: null,
            agoraUtc: new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc));

        data.Should().Be(new DateOnly(2026, 6, 2));
        origem.Should().Be(SiscanRequisicaoService.OrigemDataDoExame.Dicom);
    }

    /// <summary>Sem DICOM e sem anamnese, sobra a detecção do estudo no PACS.</summary>
    [Fact]
    public void Sem_dicom_e_sem_anamnese_cai_na_deteccao_do_pacs()
    {
        var (data, origem) = SiscanRequisicaoService.DataDoExameDe(
            dataEstudo: null, anamnesePreenchidaEm: null,
            realizadoEm: new DateTime(2026, 9, 23, 12, 3, 59, DateTimeKind.Utc),
            agoraUtc: new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc));

        data.Should().Be(new DateOnly(2026, 9, 23));
        origem.Should().Be(SiscanRequisicaoService.OrigemDataDoExame.DeteccaoNoPacs);
    }

    /// <summary>
    /// Nada disponível: usa hoje, e NUNCA a data da ficha do SISREG — que é o erro que se está
    /// corrigindo. Na prática é inalcançável, porque gerar exige anamnese.
    /// </summary>
    [Fact]
    public void Sem_nada_usa_hoje_e_nao_a_data_da_ficha()
    {
        var (data, origem) = SiscanRequisicaoService.DataDoExameDe(
            dataEstudo: null, anamnesePreenchidaEm: null, realizadoEm: null,
            agoraUtc: new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc));

        data.Should().Be(new DateOnly(2026, 9, 25));
        origem.Should().Be(SiscanRequisicaoService.OrigemDataDoExame.HojeSemNadaMelhor);
    }

    // ------------------------------------------------------------------ duplicidade

    /// <summary>
    /// A janela da crítica: 10 dias à frente, recuando um ano. Os 10 dias existem porque a
    /// requisição pode ter data de solicitação à frente; o ano para trás é o intervalo em que uma
    /// segunda mamografia da mesma paciente é suspeita.
    /// </summary>
    [Fact]
    public void Janela_de_duplicidade_vai_dez_dias_a_frente_e_um_ano_para_tras()
    {
        var (inicio, fim) = SiscanRequisicaoService.JanelaDeDuplicidade(new DateOnly(2026, 9, 22));

        fim.Should().Be(new DateOnly(2026, 10, 2));
        inicio.Should().Be(new DateOnly(2025, 10, 2));
    }

    /// <summary>Ano bissexto não pode encolher nem esticar a janela por um dia.</summary>
    [Fact]
    public void Janela_atravessa_ano_bissexto_sem_deslizar()
    {
        var (inicio, fim) = SiscanRequisicaoService.JanelaDeDuplicidade(new DateOnly(2028, 2, 25));

        fim.Should().Be(new DateOnly(2028, 3, 6));
        inicio.Should().Be(new DateOnly(2027, 3, 6));
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

    /// <summary>
    /// Ticket #139: a pergunta do SISCAN respondida na anamnese vence a dedução — mesmo contra a
    /// classificação (Alto) e os critérios, que continuam na ficha mas respondem outra pergunta.
    /// </summary>
    [Theory]
    [InlineData("sim", "01")]
    [InlineData("nao", "02")]
    [InlineData("naoSabe", "03")]
    public void Resposta_explicita_do_siscan_vence_a_deducao(string resposta, string esperado)
    {
        var campos = Agrupar(SiscanRequisicaoMapper.Montar(
            Json(
                risco: """{"classificacao":"Alto","familiar1GrauCancerMama":true}""",
                historico: """{"jaRealizouCirurgiaMamaria":{"resposta":false}}""",
                corpoSiscan: $$"""{"riscoElevado":"{{resposta}}"}"""),
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

    // --------------------------------------------- ano da última mamografia × SISCAN

    private static List<KeyValuePair<string, string>> ComAno(string ano) =>
    [
        new(SiscanRequisicaoMapper.CampoFezMamografia, SiscanRequisicaoMapper.Sim),
        new(SiscanRequisicaoMapper.CampoAnoUltimaMamografia, ano),
    ];

    /// <summary>
    /// A paciente lembrou de 2018, mas o SISCAN já tem exame dela em 2022 — e recusa "ano inferior
    /// ao da última mamografia cadastrada". O que vai é 2022, e a correção volta para avisar.
    /// </summary>
    [Fact]
    public void Ano_declarado_anterior_ao_do_siscan_sobe_para_o_dele()
    {
        var campos = ComAno("2018");

        var correcao = SiscanRequisicaoMapper.CorrigirAnoUltimaMamografia(campos, 2022);

        correcao.Should().Be(new CorrecaoAnoUltimaMamografia(2018, 2022));
        campos.Should().ContainSingle(c => c.Key == SiscanRequisicaoMapper.CampoAnoUltimaMamografia)
            .Which.Value.Should().Be("2022");
    }

    /// <summary>Igual ou depois do SUS passa como está: pode ter feito outra fora do SUS.</summary>
    [Theory]
    [InlineData("2022")]
    [InlineData("2025")]
    public void Ano_declarado_igual_ou_posterior_nao_muda(string ano)
    {
        var campos = ComAno(ano);

        SiscanRequisicaoMapper.CorrigirAnoUltimaMamografia(campos, 2022).Should().BeNull();
        campos[1].Value.Should().Be(ano);
    }

    /// <summary>Sem exame anterior no SUS a tela não traz a frase — não há contra o que corrigir.</summary>
    [Fact]
    public void Sem_ano_no_siscan_nao_mexe()
    {
        var campos = ComAno("2018");

        SiscanRequisicaoMapper.CorrigirAnoUltimaMamografia(campos, null).Should().BeNull();
        campos[1].Value.Should().Be("2018");
    }

    /// <summary>Ano em branco não é "declarado errado": não se inventa o campo.</summary>
    [Fact]
    public void Sem_ano_declarado_nao_inventa()
    {
        List<KeyValuePair<string, string>> campos =
            [new(SiscanRequisicaoMapper.CampoFezMamografia, SiscanRequisicaoMapper.Nao)];

        SiscanRequisicaoMapper.CorrigirAnoUltimaMamografia(campos, 2022).Should().BeNull();
        campos.Should().ContainSingle();
    }

    [Fact]
    public void Correcao_na_anamnese_troca_o_ano_guarda_o_declarado_e_preserva_o_resto()
    {
        var conteudo = """
            {"historicoClinico":{"jaRealizouMamografia":{"resposta":true}},
             "siscan":{"anoUltimaMamografia":"2018","riscoElevado":"nao"}}
            """;

        var corrigido = System.Text.Json.Nodes.JsonNode.Parse(
            SiscanRequisicaoMapper.AplicarCorrecaoNoConteudo(conteudo, new(2018, 2022)))!;

        corrigido["siscan"]!["anoUltimaMamografia"]!.GetValue<string>().Should().Be("2022");
        corrigido["siscan"]!["anoUltimaMamografiaDeclarado"]!.GetValue<string>().Should().Be("2018");
        corrigido["siscan"]!["riscoElevado"]!.GetValue<string>().Should().Be("nao");
        corrigido["historicoClinico"]!["jaRealizouMamografia"]!["resposta"]!.GetValue<bool>().Should().BeTrue();
    }

    // ------------------------------------------------ unidade requisitante escolhida

    private static readonly List<SMSMais.Core.Integracoes.SiscanWeb.SiscanHtml.UnidadeRequisitante> DaConta =
    [
        new("1", "2266741", "AMBULATORIO PERICLES SIQUEIRA FERREIRA"),
        new("8", "6886973", "SECRETARIA MUNICIPAL DE SAUDE DE MARICA"),
        new("14", "2266881", "UNIDADE DE SAUDE DA FAMILIA CENTRAL"),
    ];

    /// <summary>O caso normal: a unidade do pedido está na conta e é por ela que sai.</summary>
    [Fact]
    public void Unidade_do_pedido_na_conta_e_a_que_vai()
    {
        SiscanRequisicaoService.UnidadeParaEnviar(DaConta, "2266881", null)!
            .Valor.Should().Be("14");
        SiscanRequisicaoService.UnidadeParaEnviar(DaConta, "2266881", "2266881")!
            .Valor.Should().Be("14");
    }

    /// <summary>Com a do pedido disponível, o servidor recusa outra — a escolha é só para quando falta.</summary>
    [Fact]
    public void Com_a_do_pedido_na_conta_outra_e_recusada()
    {
        var acao = () => SiscanRequisicaoService.UnidadeParaEnviar(DaConta, "2266881", "6886973");

        acao.Should().Throw<SMSMais.Core.Common.Excecoes.ValidacaoException>()
            .Which.Erros.Should().ContainKey("siscan.unidade_do_pedido_disponivel");
    }

    /// <summary>Fora da conta e sem escolha: null — a tela mostra a lista em vez de dar erro.</summary>
    [Fact]
    public void Fora_da_conta_sem_escolha_devolve_null()
    {
        SiscanRequisicaoService.UnidadeParaEnviar(DaConta, "9999999", null).Should().BeNull();
        SiscanRequisicaoService.UnidadeParaEnviar(DaConta, "9999999", "  ").Should().BeNull();
    }

    [Fact]
    public void Fora_da_conta_vai_pela_escolhida()
    {
        SiscanRequisicaoService.UnidadeParaEnviar(DaConta, "9999999", " 6886973 ")!
            .Should().Be(DaConta[1]);
    }

    /// <summary>Escolheu uma que não está na lista (a conta mudou, ou chamada forjada): recusa.</summary>
    [Fact]
    public void Escolhida_fora_da_lista_e_recusada()
    {
        var acao = () => SiscanRequisicaoService.UnidadeParaEnviar(DaConta, "9999999", "1234567");

        acao.Should().Throw<SMSMais.Core.Common.Excecoes.ValidacaoException>()
            .Which.Erros.Should().ContainKey("siscan.unidade_indisponivel");
    }

    [Fact]
    public void Unidade_escolhida_fica_registrada_na_anamnese_sem_mexer_no_resto()
    {
        var conteudo = """{"siscan":{"anoUltimaMamografia":"2022","riscoElevado":"nao"},"queixas":{}}""";
        var unidade = new UnidadeRequisitanteEscolhida(
            "6886973", "SECRETARIA MUNICIPAL DE SAUDE DE MARICA", "9999999", "USF EXEMPLO",
            "Operadora Exemplo", new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc));

        var json = System.Text.Json.Nodes.JsonNode.Parse(
            SiscanRequisicaoMapper.AnotarUnidadeNoConteudo(conteudo, unidade))!;
        var bloco = json["siscan"]!["unidadeRequisitanteEscolhida"]!;

        bloco["cnes"]!.GetValue<string>().Should().Be("6886973");
        bloco["nome"]!.GetValue<string>().Should().Be("SECRETARIA MUNICIPAL DE SAUDE DE MARICA");
        bloco["cnesDoPedido"]!.GetValue<string>().Should().Be("9999999");
        bloco["nomeDoPedido"]!.GetValue<string>().Should().Be("USF EXEMPLO");
        bloco["escolhidaPor"]!.GetValue<string>().Should().Be("Operadora Exemplo");
        bloco["escolhidaEm"]!.GetValue<string>().Should().StartWith("2026-09-30T14:00:00");
        json["siscan"]!["anoUltimaMamografia"]!.GetValue<string>().Should().Be("2022");
        json["siscan"]!["riscoElevado"]!.GetValue<string>().Should().Be("nao");
    }

    // ------------------------------------------------ recusa no Avançar

    private static readonly List<SMSMais.Core.Integracoes.SiscanWeb.SiscanHtml.CampoCadsus> CadsusSemRaca =
    [
        new("frm:nomeMae", "Nome da Mãe", false),
        new("frm:racaCor", "Raça/Cor", true),
    ];

    /// <summary>
    /// O caso de 30/09/2026: o SISCAN recusou por Raça/Cor. A resposta diz qual campo, que ele
    /// vem do CADSUS e onde se corrige — e leva o texto deles junto.
    /// </summary>
    [Fact]
    public void Recusa_que_cita_campo_do_cadsus_manda_corrigir_no_cadsusweb()
    {
        var (codigo, mensagem) = SiscanRequisicaoService.ExplicarRecusaNoAvancar(
            ["O campo Raça/Cor é obrigatório."], CadsusSemRaca, ["NOVO EXAME"]);

        codigo.Should().Be("siscan.cadastro_cadsus_incompleto");
        mensagem.Should().Contain("O campo Raça/Cor é obrigatório.")
            .And.Contain("O campo Raça/Cor vem do cadastro nacional")
            .And.Contain("CADSUSWEB")
            .And.NotContain("Nome da Mãe");
    }

    /// <summary>Recusa muda (sem mensagem) com campo do CADSUS vazio: o vazio é a evidência.</summary>
    [Fact]
    public void Recusa_sem_mensagem_culpa_o_campo_vazio()
    {
        var (codigo, mensagem) = SiscanRequisicaoService.ExplicarRecusaNoAvancar(
            [], CadsusSemRaca, ["NOVO EXAME"]);

        codigo.Should().Be("siscan.cadastro_cadsus_incompleto");
        mensagem.Should().StartWith("O campo Raça/Cor vem do cadastro nacional");
    }

    /// <summary>
    /// Mensagem que não cita campo do CADSUS não é culpa do CADSUS, mesmo havendo campo vazio —
    /// mandar a pessoa ao CADSUSWEB por engano é pior que mostrar o texto deles.
    /// </summary>
    [Fact]
    public void Recusa_por_outro_motivo_nao_culpa_o_cadsus()
    {
        var (codigo, mensagem) = SiscanRequisicaoService.ExplicarRecusaNoAvancar(
            ["O campo Tipo de Exame deve ser informado"], CadsusSemRaca, ["NOVO EXAME"]);

        codigo.Should().Be("siscan.avancar_recusado");
        mensagem.Should().Contain("O campo Tipo de Exame deve ser informado").And.NotContain("CADSUS");
    }

    /// <summary>Raça/Cor preenchida com valor que o SISCAN não aceita: citada na mensagem, vale igual.</summary>
    [Fact]
    public void Campo_do_cadsus_citado_mesmo_preenchido_conta()
    {
        List<SMSMais.Core.Integracoes.SiscanWeb.SiscanHtml.CampoCadsus> preenchidos =
            [new("frm:racaCor", "Raça/Cor", false), new("frm:nomeMae", "Nome da Mãe", false)];

        var (codigo, mensagem) = SiscanRequisicaoService.ExplicarRecusaNoAvancar(
            ["Raça/Cor inválida", "Nome da Mãe incompleto"], preenchidos, []);

        codigo.Should().Be("siscan.cadastro_cadsus_incompleto");
        mensagem.Should().Contain("Os campos Raça/Cor, Nome da Mãe vêm do cadastro nacional");
    }

    // ------------------------------------------------ raça/cor pedida pelo SISCAN

    private static readonly List<SMSMais.Core.Integracoes.SiscanWeb.SiscanHtml.OpcaoSiscan> Racas =
    [
        new("1", "BRANCA"), new("2", "PRETA"), new("3", "PARDA"), new("4", "AMARELA"), new("5", "INDIGENA"),
    ];

    [Fact]
    public void Raca_cor_escolhida_e_sempre_uma_das_opcoes_do_siscan()
    {
        SiscanRequisicaoService.OpcaoEscolhida(Racas, null, "siscan.raca_cor_invalida", "Raça/Cor")
            .Should().BeNull();
        SiscanRequisicaoService.OpcaoEscolhida(Racas, " 3 ", "siscan.raca_cor_invalida", "Raça/Cor")
            .Should().Be(new SMSMais.Core.Integracoes.SiscanWeb.SiscanHtml.OpcaoSiscan("3", "PARDA"));

        var inventada = () => SiscanRequisicaoService.OpcaoEscolhida(
            Racas, "99", "siscan.raca_cor_invalida", "Raça/Cor");
        inventada.Should().Throw<SMSMais.Core.Common.Excecoes.ValidacaoException>()
            .Which.Erros.Should().ContainKey("siscan.raca_cor_invalida");
    }

    [Fact]
    public void Raca_cor_informada_fica_registrada_na_anamnese()
    {
        var raca = new RacaCorInformada(
            "5", "INDIGENA", "444", "XAVANTE (A'UWE, AKWE, AWEN, AKWEN)", "Operadora Exemplo",
            new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc));

        var json = System.Text.Json.Nodes.JsonNode.Parse(
            SiscanRequisicaoMapper.AnotarRacaCorNoConteudo("""{"siscan":{"riscoElevado":"nao"}}""", raca))!;
        var bloco = json["siscan"]!["racaCorInformada"]!;

        bloco["codigo"]!.GetValue<string>().Should().Be("5");
        bloco["rotulo"]!.GetValue<string>().Should().Be("INDIGENA");
        bloco["etniaCodigo"]!.GetValue<string>().Should().Be("444");
        bloco["etniaRotulo"]!.GetValue<string>().Should().StartWith("XAVANTE");
        bloco["informadaPor"]!.GetValue<string>().Should().Be("Operadora Exemplo");
        json["siscan"]!["riscoElevado"]!.GetValue<string>().Should().Be("nao");
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

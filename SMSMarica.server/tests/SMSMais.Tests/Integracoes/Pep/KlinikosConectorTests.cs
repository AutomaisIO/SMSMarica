using System.Globalization;
using Hl7.Fhir.Model;
using SMSMarica.Core.Integracoes.Pep.Estrategias;
using SMSMarica.Core.Integracoes.Pep.Estrategias.Klinikos;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Tests.Integracoes.Pep;

/// <summary>
/// Conector Klinikos — as regras que, se quebrarem, corrompem prontuário em silêncio.
/// O mapeamento medido está em <c>docs/klinikos/mapeamento-fhir.md</c>.
/// </summary>
public class KlinikosConectorTests
{
    private static KlinikosFhirMapper Mapper(string slug = "upa24h-marica") =>
        new(slug, $"https://smsmarica.saude.marica/source/klinikos/{slug}");

    private static PacienteLinha Pac(string cod = "062512190087", string? cpf = "12345678909",
        string? cns = null, string? nome = "FULANO DE TAL") =>
        new(cod, nome, cpf, cns, "1980-05-10T00:00:00", "M", "MAE", "PAI",
            "2126000000", "21990000000", null, null, null, null, null, 226712823);

    private static BoletimLinha Bol(string cod = "0006202608030001", string? pac = "062512190087",
        string? unid = "0006") =>
        new(cod, pac, unid, "2026-08-03T21:12:00", "2026-08-03T21:00:00", null, null, "1", "3", 990);

    private static EvolucaoLinha Evo(string tipo, string? cid = null, long cod = 4242) =>
        new(cod, "0006202608030001", tipo, "2026-08-03T21:30:00", "texto da nota", "0001", cid, null, 991);

    // ---------------- identidade da unidade (ADR-0039) ----------------

    /// <summary>
    /// A ponte que o ADR-0039 depende: a UPA do Klinikos e a do Salux têm nomes diferentes e o
    /// MESMO CNES (7164440, medido nos dois lados). Se o CNES deixar de sair como identifier
    /// nacional, o hub passa a ter duas Organizations para uma unidade só.
    /// </summary>
    [Fact]
    public void CNES_da_unidade_entra_como_identificador_nacional()
    {
        var o = Mapper().BuildOrganization(
            new UnidadeLinha("0006", "UPA MARICA", "UPA MARICA", "UPAMARICA", "7164440", null, null));

        Assert.Equal("7164440",
            Assert.Single(o.Identifier, i => i.System == KlinikosFhirMapper.IdentCnes).Value);
        Assert.Equal("upa24h-marica:0006",
            Assert.Single(o.Identifier, i => i.System == KlinikosFhirMapper.IdentUnidade).Value);
    }

    /// <summary>
    /// O SQL tem de ler <c>unid_codigoCNES</c>. Existe também <c>unidade_municipioCNES</c> na
    /// mesma tabela, e ela está VAZIA — ler a coluna errada faria toda unidade entrar sem CNES,
    /// e a ponte entre PEPs simplesmente não existiria.
    /// </summary>
    [Fact]
    public void O_SQL_de_unidades_le_a_coluna_de_CNES_certa()
    {
        var sql = KlinikosImportacaoStrategy.SqlUnidades();

        Assert.Contains("unid_codigoCNES", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unidade_municipioCNES", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unidade_sem_CNES_entra_sem_identifier_vazio()
    {
        var o = Mapper().BuildOrganization(new UnidadeLinha("0009", "UNIDADE NOVA", null, null, null, null, null));

        Assert.DoesNotContain(o.Identifier, i => string.IsNullOrWhiteSpace(i.Value));
        Assert.Single(o.Identifier);
    }

    // ---------------- identidade do paciente ----------------

    [Fact]
    public void Paciente_com_CPF_entra_ancorado_no_CPF_e_sem_marca()
    {
        var p = Mapper().BuildPatient(Pac());

        Assert.Equal("12345678909",
            Assert.Single(p.Identifier, i => i.System == KlinikosFhirMapper.IdentCpf).Value);
        Assert.DoesNotContain(p.Meta?.Tag ?? [], t => t.Code == "identidade-incompleta");
    }

    /// <summary>
    /// 16,9% do cadastro da UPA (14.152 pessoas, TODAS com atendimento) não tem CPF nem CNS.
    /// Elas entram — mas marcadas, e sem identifier vazio: um <c>value</c> em branco faz o hub
    /// rejeitar o recurso inteiro com 400, que foi exatamente como essa funcionalidade nasceu
    /// quebrada no conector do Salux.
    /// </summary>
    [Fact]
    public void Paciente_sem_CPF_entra_marcado_e_sem_identifier_vazio()
    {
        var p = Mapper().BuildPatient(Pac(cpf: null));

        Assert.DoesNotContain(p.Identifier, i => string.IsNullOrWhiteSpace(i.Value));
        Assert.DoesNotContain(p.Identifier, i => i.System == KlinikosFhirMapper.IdentCpf);
        Assert.Contains(p.Meta!.Tag, t => t.System == "urn:smsmarica:qualidade" && t.Code == "identidade-incompleta");
    }

    /// <summary>A tag é a MESMA do Salux: a pergunta "quantos registros incertos temos?" tem
    /// uma resposta só no hub inteiro, não uma por base.</summary>
    [Fact]
    public void A_marca_de_identidade_incompleta_e_a_mesma_do_outro_conector()
    {
        var klinikos = Mapper().BuildPatient(Pac(cpf: null)).Meta!.Tag.Single();

        var salux = new Patient();
        SMSMarica.Core.Integracoes.Pep.Estrategias.Salux.SaluxFhirMapper.MarcarIdentidadeIncompleta(salux);

        Assert.Equal(salux.Meta!.Tag.Single().System, klinikos.System);
        Assert.Equal(salux.Meta.Tag.Single().Code, klinikos.Code);
    }

    /// <summary>Duas instâncias, mesmos códigos internos — o slug é o que impede a colisão.</summary>
    [Fact]
    public void Codigo_interno_e_prefixado_pela_instancia()
    {
        var upa = Mapper("upa24h-marica").BuildPatient(Pac());
        var sta = Mapper("santarita-marica").BuildPatient(Pac());

        Assert.Equal("upa24h-marica:062512190087",
            Assert.Single(upa.Identifier, i => i.System == KlinikosFhirMapper.IdentPaciente).Value);
        Assert.Equal("santarita-marica:062512190087",
            Assert.Single(sta.Identifier, i => i.System == KlinikosFhirMapper.IdentPaciente).Value);
    }

    // ---------------- Encounter ----------------

    [Fact]
    public void Boletim_vira_Encounter_de_emergencia_na_unidade_onde_aconteceu()
    {
        var e = Mapper().BuildEncounter(Bol(), "Patient/abc", "Organization/xyz", teveAtendimento: true);

        Assert.Equal("EMER", e.Class.Code);
        Assert.Equal("Organization/xyz", e.ServiceProvider!.Reference);
        Assert.Equal(Encounter.EncounterStatus.Finished, e.Status);
        Assert.StartsWith("2026-08-03T21:12:00", e.Period!.Start, StringComparison.Ordinal);
    }

    /// <summary>
    /// 7,7% dos boletins não chegam a ter atendimento (evasão). Eles PRECISAM entrar: a pessoa
    /// esteve na unidade, e isso é informação clínica — some do hub se o conector filtrar.
    /// </summary>
    [Fact]
    public void Boletim_sem_atendimento_entra_com_status_proprio()
    {
        var e = Mapper().BuildEncounter(Bol(), "Patient/abc", null, teveAtendimento: false);

        Assert.Equal(Encounter.EncounterStatus.Cancelled, e.Status);
        Assert.Null(e.ServiceProvider);
    }

    /// <summary>
    /// O fechamento é o único sinal de que a pessoa foi embora — <c>status=finished</c> é gravado
    /// desde a primeira evolução, com o paciente ainda na unidade. Sem <c>period.end</c> o hub
    /// não sabe distinguir quem saiu de quem está lá, que era o estado até 08/08/2026.
    /// </summary>
    [Fact]
    public void Boletim_fechado_carrega_a_hora_da_saida()
    {
        var e = Mapper().BuildEncounter(Bol(), "Patient/abc", null, teveAtendimento: true,
            new DesfechoBoletim("2026-08-04T01:40:00", 17, "A.1 - Atendimento em consultório concluído", null));

        Assert.StartsWith("2026-08-04T01:40:00", e.Period!.End, StringComparison.Ordinal);
    }

    /// <summary>Boletim ainda aberto não pode ganhar fim inventado — nem desfecho.</summary>
    [Fact]
    public void Boletim_sem_fechamento_fica_sem_fim_e_sem_desfecho()
    {
        var e = Mapper().BuildEncounter(Bol(), "Patient/abc", null, teveAtendimento: true);

        Assert.Null(e.Period!.End);
        Assert.Null(e.Hospitalization);
    }

    /// <summary>
    /// A saída fica no ValueSet do R4 <b>e</b> preserva o código da origem: a pesquisa de
    /// satisfação precisa separar evasão de alta, e o R4 achata as duas em <c>aadvice</c>.
    /// </summary>
    [Theory]
    [InlineData(17, "home")]
    [InlineData(1, "home")]
    [InlineData(14, "home")]
    [InlineData(3, "aadvice")]
    [InlineData(12, "aadvice")]
    [InlineData(5, "other-hcf")]
    [InlineData(6, "exp")]
    [InlineData(8, "exp")]
    [InlineData(9, "oth")]
    public void Tipo_de_saida_da_origem_vira_discharge_disposition_do_R4(int tipsai, string esperado)
    {
        var e = Mapper().BuildEncounter(Bol(), "Patient/abc", null, teveAtendimento: true,
            new DesfechoBoletim("2026-08-04T01:40:00", tipsai, "descrição da origem", null));

        var cc = e.Hospitalization!.DischargeDisposition!;
        Assert.Equal(esperado,
            Assert.Single(cc.Coding, c => c.System == "http://terminology.hl7.org/CodeSystem/discharge-disposition").Code);
        Assert.Equal(tipsai.ToString(CultureInfo.InvariantCulture),
            Assert.Single(cc.Coding, c => c.System == "urn:klinikos:tiposaida").Code);
        Assert.Equal("descrição da origem", cc.Text);
    }

    /// <summary>
    /// 4,9% dos boletins fecham sem passar pelo médico — têm hora de saída e não têm tipo. Ficar
    /// sem desfecho é diferente de ficar sem saída; o Encounter tem de sair com um e sem o outro.
    /// </summary>
    [Fact]
    public void Fechamento_sem_tipo_de_saida_ainda_registra_a_saida()
    {
        var e = Mapper().BuildEncounter(Bol(), "Patient/abc", null, teveAtendimento: true,
            new DesfechoBoletim("2026-08-04T01:40:00", null, null, null));

        Assert.StartsWith("2026-08-04T01:40:00", e.Period!.End, StringComparison.Ordinal);
        Assert.Null(e.Hospitalization);
    }

    /// <summary>
    /// O fechamento vem de <c>atendimento_ambulatorial</c>, não do <c>Pronto_Atendimento</c>, e
    /// <b>não</b> de <c>upaatemed_DataSaida</c> — campo com nome certo e 6,0% de preenchimento.
    /// </summary>
    [Fact]
    public void Consulta_de_desfecho_le_a_coluna_medida_e_nao_a_homonima()
    {
        var sql = KlinikosImportacaoStrategy.SqlDesfechos(["0006202608030001"]);

        Assert.Contains("atendamb_datafinal", sql, StringComparison.Ordinal);
        Assert.Contains("FROM atendimento_ambulatorial", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("upaatemed_DataSaida", sql, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------- evolução ----------------

    [Theory]
    [InlineData("REAVALIAÇÃO", "REAVALIACAO")]
    [InlineData("PRESCRIÇÃO", "PRESCRICAO")]
    [InlineData("INÍCIO DO ATENDIMENTO MÉDICO", "INICIO DO ATENDIMENTO MEDICO")]
    public void Tipo_da_evolucao_e_normalizado_sem_acento(string bruto, string esperado) =>
        Assert.Equal(esperado, Evo(bruto).TipoNorm);

    /// <summary>
    /// A Condition é do BOLETIM, não da linha de evolução. A reavaliação REVISA o diagnóstico
    /// da mesma passagem: uma Condition por evolução encheria o prontuário de diagnósticos
    /// concorrentes para um atendimento só — 184 mil reavaliações contra 165 mil atendimentos.
    /// </summary>
    [Fact]
    public void Condition_e_uma_por_boletim__a_reavaliacao_revisa_e_nao_soma()
    {
        var m = Mapper();
        var inicio = m.BuildCondition("0006202608030001", "J06.9", "Patient/a", "Encounter/e")!;
        var revisao = m.BuildCondition("0006202608030001", "J18.9", "Patient/a", "Encounter/e")!;

        Assert.Equal(inicio.Identifier[0].Value, revisao.Identifier[0].Value);
        Assert.Equal("J18.9", revisao.Code!.Coding[0].Code);
    }

    /// <summary>CID-10 cruz-estrela: a origem grava "L14 *", e <c>code</c> não aceita espaço nem asterisco.</summary>
    [Fact]
    public void CID_com_marcador_cruz_estrela_e_limpo_mas_o_original_fica_no_texto()
    {
        var c = Mapper().BuildCondition("B1", "L14 *", "Patient/a", "Encounter/e")!;

        Assert.Equal("L14", c.Code!.Coding[0].Code);
        Assert.Equal("L14 *", c.Code.Text);
    }

    [Fact]
    public void Evolucao_sem_CID_nao_inventa_diagnostico() =>
        Assert.Null(Mapper().BuildCondition("B1", null, "Patient/a", "Encounter/e"));

    // ---------------- sinais vitais ----------------

    private static SinaisVitaisLinha Sv(string? pa = "120/80", string? pulso = "88",
        string? temp = "36.5", string? sat = null, string? peso = null) =>
        new(777, "0006202608030001", "2026-08-03T21:20:00", "0001", pa, pulso, temp, null, null, sat, peso, 992);

    [Fact]
    public void Pressao_arterial_vira_painel_com_sistolica_e_diastolica()
    {
        var obs = Mapper().BuildObservacoesVitais(Sv(), "Patient/a", "Encounter/e");
        var pa = obs.Single(o => o.Obs.Code!.Coding[0].Code == "85354-9").Obs;

        Assert.Equal(2, pa.Component.Count);
        Assert.Equal(120m, ((Quantity)pa.Component[0].Value!).Value);
        Assert.Equal(80m, ((Quantity)pa.Component[1].Value!).Value);
    }

    /// <summary>Campo em branco ou sem número ("N/A") não pode virar Observation vazia — isso
    /// seria inventar medida que ninguém aferiu.</summary>
    [Fact]
    public void Medida_ausente_ou_nao_numerica_nao_vira_Observation()
    {
        var obs = Mapper().BuildObservacoesVitais(
            Sv(pa: "N/A", pulso: "88", temp: null, sat: "", peso: "-"), "Patient/a", "Encounter/e");

        Assert.Single(obs);
        Assert.Equal("8867-4", obs[0].Obs.Code!.Coding[0].Code);
    }

    /// <summary>Uma Observation por (linha de sinais, campo): reimport atualiza, não duplica.</summary>
    [Fact]
    public void Cada_medida_tem_identifier_determinista_e_distinto()
    {
        var chaves = Mapper().BuildObservacoesVitais(Sv(), "Patient/a", "Encounter/e")
            .Select(o => o.Chave).ToList();

        Assert.Equal(chaves.Count, chaves.Distinct().Count());
        Assert.All(chaves, c => Assert.StartsWith("upa24h-marica:777:", c, StringComparison.Ordinal));
    }

    // ---------------- higiene do dado da origem ----------------

    /// <summary>
    /// A recepção preenche o campo obrigatório com lixo quando o paciente não informa telefone
    /// — <c>0000000000</c> está no PRIMEIRO registro da base. Telefone falso é pior que nenhum:
    /// alguém tenta ligar, e o paciente entra em relatório de "contactável" sem ser.
    /// </summary>
    [Theory]
    [InlineData("0000000000")]
    [InlineData("9999999999")]
    [InlineData("123")]
    [InlineData("")]
    public void Telefone_de_preenchimento_obrigatorio_nao_entra(string lixo)
    {
        var pac = Pac() with { Telefone = lixo, Celular = null };

        Assert.DoesNotContain(Mapper().BuildPatient(pac).Telecom,
            t => t.System == ContactPoint.ContactPointSystem.Phone);
    }

    [Fact]
    public void Telefone_de_verdade_entra()
    {
        var pac = Pac() with { Telefone = "2126000000", Celular = "21993094621" };

        Assert.Equal(2, Mapper().BuildPatient(pac).Telecom
            .Count(t => t.System == ContactPoint.ContactPointSystem.Phone));
    }

    /// <summary>
    /// <c>profissional</c> é a ÚNICA das tabelas lidas sem <c>rv_atualizacao</c> — medido contra
    /// a base. Um SQL que corte por rowversion ali falha com "Invalid column name" e derruba a
    /// fase inteira no primeiro run.
    /// </summary>
    [Fact]
    public void O_SQL_de_profissionais_nao_corta_por_rowversion()
    {
        var sql = KlinikosImportacaoStrategy.SqlProfissionais();

        Assert.DoesNotContain("rv_atualizacao", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TOP", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PROF_CODIGO", sql, StringComparison.Ordinal);
    }

    // ---------------- CDC e paginação ----------------

    [Fact]
    public void Ponteiro_da_fase_so_avanca__nunca_retrocede()
    {
        var m = new MarcaDagua();
        m.AvancarPonteiro("paciente", 500);
        m.AvancarPonteiro("paciente", 300);

        Assert.Equal(500, m.Ponteiro("paciente"));
        Assert.Equal(0, m.Ponteiro("atendimento"));
    }

    [Fact]
    public void O_SQL_corta_por_rowversion_e_ordena_por_ele()
    {
        var sql = KlinikosImportacaoStrategy.SqlPacientes(226712823, 2000);

        Assert.Contains("rv_atualizacao) > 226712823", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY CONVERT(BIGINT, rv_atualizacao)", sql, StringComparison.Ordinal);
        Assert.Contains("TOP 2000", sql, StringComparison.Ordinal);
    }

    /// <summary>T-SQL não tem tuple-IN e trava em 2.100 parâmetros — o lote é obrigatório, e
    /// estourá-lo tem de explodir aqui, não no meio de um run em produção.</summary>
    [Fact]
    public void Lote_de_IN_acima_do_limite_seguro_e_recusado()
    {
        var grande = Enumerable.Range(0, 1_001).Select(i => i.ToString()).ToList();

        Assert.Throws<ArgumentException>(() => KlinikosImportacaoStrategy.ListaTexto(grande));
        Assert.Equal("'a','b'", KlinikosImportacaoStrategy.ListaTexto(["a", "b"]));
    }

    [Fact]
    public void Aspa_no_codigo_e_escapada() =>
        Assert.Equal("'O''BRIEN'", KlinikosImportacaoStrategy.ListaTexto(["O'BRIEN"]));

    // ---------------- resolução da estratégia ----------------

    /// <summary>
    /// <c>TipoFonte.SqlServer</c> é o TIPO DE BANCO, não o produto de PEP. Sem a família como
    /// discriminador, um prontuário diferente rodando sobre SQL Server casaria com a estratégia
    /// do Klinikos e leria tabelas que não existem lá.
    /// </summary>
    [Fact]
    public void A_estrategia_atende_pela_familia__nao_so_pelo_tipo_de_banco()
    {
        IEstrategiaImportacaoPep e = new KlinikosImportacaoStrategy(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<KlinikosImportacaoStrategy>.Instance);

        Assert.True(e.Atende(new IaFonte { Tipo = TipoFonte.SqlServer, Familia = "klinikos" }));
        Assert.True(e.Atende(new IaFonte { Tipo = TipoFonte.SqlServer, Familia = " Klinikos " }));
        Assert.False(e.Atende(new IaFonte { Tipo = TipoFonte.SqlServer, Familia = "outro-pep" }));
        Assert.False(e.Atende(new IaFonte { Tipo = TipoFonte.SqlServer, Familia = null }));
        Assert.False(e.Atende(new IaFonte { Tipo = TipoFonte.Salux, Familia = "klinikos" }));
    }
}

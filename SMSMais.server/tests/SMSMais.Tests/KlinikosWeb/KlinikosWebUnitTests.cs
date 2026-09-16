using SMSMais.Core.Integracoes.KlinikosWeb;
using SMSMais.Core.Integracoes.KlinikosWeb.Cid;
using SMSMais.Core.Integracoes.KlinikosWeb.Fhir;

namespace SMSMais.Tests.KlinikosWeb;

/// <summary>
/// Testes PUROS (sem Docker) do conector web do Klinikos: as armadilhas de parsing medidas no
/// laboratório (zero à esquerda do boletim), o de-para de CID (texto→código) e a forma dos
/// recursos FHIR (identifier/período/CID) que o teste de paridade depende que batam com o SQL.
/// </summary>
public class KlinikosWebUnitTests
{
    // ---------------- boletim: o zero à esquerda ----------------

    [Theory]
    [InlineData("52609150001", "052609150001")]   // 407 come o zero (11 díg) → normaliza a 12
    [InlineData("052609150001", "052609150001")]  // 667 já vem 12 → intacto
    [InlineData(" 052609150001 ", "052609150001")]
    [InlineData("052.609.150001", "052609150001")] // só dígitos
    public void NormalizarBoletim_padroniza_para_12_digitos(string bruto, string esperado) =>
        KlinikosRelatorioParser.NormalizarBoletim(bruto).Should().Be(esperado);

    [Theory]
    [InlineData("123")]        // curto demais = totalizador/ruído
    [InlineData("")]
    [InlineData("Total")]
    [InlineData(null)]
    public void NormalizarBoletim_recusa_o_que_nao_e_boletim(string? bruto) =>
        KlinikosRelatorioParser.NormalizarBoletim(bruto).Should().BeNull();

    // ---------------- de-para de CID ----------------

    private static CidDeParaService ComCatalogo()
    {
        var svc = new CidDeParaService();
        svc.CarregarCatalogo(
        [
            ("J00", "NASOFARINGITE AGUDA [RESFRIADO COMUM]"),
            ("J189", "PNEUMONIA NAO ESPECIFICADA"),
        ]);
        return svc;
    }

    [Fact]
    public void Cid_casa_por_descricao_ignorando_sufixo_entre_colchetes()
    {
        var r = ComCatalogo().Resolver("Nasofaringite Aguda [resfriado comum]");
        r.Mapeado.Should().BeTrue();
        r.Codigo.Should().Be("J00");
        r.Texto.Should().Be("Nasofaringite Aguda [resfriado comum]");
    }

    [Fact]
    public void Cid_sem_correspondencia_nao_inventa_codigo()
    {
        var r = ComCatalogo().Resolver("DIAGNOSTICO QUE NAO EXISTE NO CATALOGO");
        r.Mapeado.Should().BeFalse();
        r.Codigo.Should().BeNull();
        r.Texto.Should().Be("DIAGNOSTICO QUE NAO EXISTE NO CATALOGO");
    }

    [Fact]
    public void Cid_reconhece_codigo_explicito_no_texto()
    {
        var r = new CidDeParaService().Resolver("J00 - NASOFARINGITE AGUDA");
        r.Mapeado.Should().BeTrue();
        r.Codigo.Should().Be("J00");
    }

    [Fact]
    public void Cid_vazio_devolve_nada()
    {
        var r = new CidDeParaService().Resolver("   ");
        r.Codigo.Should().BeNull();
        r.Mapeado.Should().BeFalse();
    }

    [Fact]
    public void NormalizarDescricao_tira_acento_colchete_e_pontuacao()
    {
        CidDeParaService.NormalizarDescricao("Nasofaringite Aguda [Resfriado Comum]")
            .Should().Be("NASOFARINGITE AGUDA");
    }

    // ---------------- chegada / nascimento ----------------

    [Theory]
    [InlineData("15/09/2026 14:30", "2026-09-15T14:30:00")]
    [InlineData("15/09/2026 14:30:00", "2026-09-15T14:30:00")]
    [InlineData("15/09/2026", "2026-09-15T00:00:00")]
    public void ChegadaIso_converte_ddMMyyyy_para_iso_local(string bruto, string esperado) =>
        KlinikosWebFhirMapper.ChegadaIso(bruto).Should().Be(esperado);

    [Theory]
    [InlineData("46280")]      // serial de Excel — não é data legível
    [InlineData("50 anos")]
    [InlineData(null)]
    public void ChegadaIso_recusa_o_que_nao_e_data(string? bruto) =>
        KlinikosWebFhirMapper.ChegadaIso(bruto).Should().BeNull();

    [Fact]
    public void DataNascimento_extrai_data_quando_legivel()
    {
        KlinikosWebFhirMapper.DataNascimento("23/06/1975 51").Should().Be("1975-06-23");
        KlinikosWebFhirMapper.DataNascimento("46280").Should().BeNull();
    }

    // ---------------- forma dos recursos FHIR (paridade) ----------------

    private static EspinhaRegistro Espinha(string spa, string? cor = "Verde", string? chegada = "15/09/2026 14:30") =>
        new(spa, chegada, cor, "CLINICA MEDICA", "FULANO DE TAL", "46280", "23/06/1975 51", "Urgência", true, true);

    [Fact]
    public void Encounter_web_tem_a_mesma_forma_do_sql()
    {
        var mapper = new KlinikosWebFhirMapper("klinikos-conde", "src/klinikos/klinikos-conde", new CidDeParaService());
        var enc = mapper.MontarEncounter(Espinha("052609150001"), "Patient/abc", null, "0005");

        enc.Class.Code.Should().Be("EMER");
        enc.Identifier.Should().ContainSingle();
        enc.Identifier[0].System.Should().Be("urn:klinikos:boletim");
        enc.Identifier[0].Value.Should().Be("klinikos-conde:052609150001");
        enc.Period!.Start.Should().Be("2026-09-15T14:30:00-03:00");
        enc.Subject!.Reference.Should().Be("Patient/abc");
    }

    [Fact]
    public void Condition_web_usa_codigo_do_depara_e_o_mesmo_identifier()
    {
        var mapper = new KlinikosWebFhirMapper("klinikos-conde", "src/klinikos/klinikos-conde", ComCatalogo());
        var cond = mapper.MontarCondition("052609150001", "NASOFARINGITE AGUDA [RESFRIADO COMUM]",
            "Patient/abc", "Encounter/xyz", out var mapeado);

        mapeado.Should().BeTrue();
        cond!.Identifier[0].Value.Should().Be("klinikos-conde:052609150001:cond");
        cond.Code!.Coding.Should().ContainSingle();
        cond.Code.Coding![0].Code.Should().Be("J00");
        cond.Code.Text.Should().Be("NASOFARINGITE AGUDA [RESFRIADO COMUM]");
    }

    [Fact]
    public void Condition_sem_codigo_fica_text_only_e_marca_nao_mapeado()
    {
        var mapper = new KlinikosWebFhirMapper("klinikos-conde", "src/klinikos/klinikos-conde", new CidDeParaService());
        var cond = mapper.MontarCondition("052609150001", "DOR ABDOMINAL SEM CATALOGO",
            "Patient/abc", "Encounter/xyz", out var mapeado);

        mapeado.Should().BeFalse();
        cond!.Code!.Coding.Should().BeNullOrEmpty();
        cond.Code.Text.Should().Be("DOR ABDOMINAL SEM CATALOGO");
    }

    [Fact]
    public void Condition_sem_cid_devolve_null()
    {
        var mapper = new KlinikosWebFhirMapper("klinikos-conde", "src/klinikos/klinikos-conde", new CidDeParaService());
        mapper.MontarCondition("052609150001", null, "Patient/abc", "Encounter/xyz", out _)
            .Should().BeNull();
    }

    [Fact]
    public void Instancia_conde_e_fonte_web_primaria_upa_nao()
    {
        KlinikosWebInstanciaFhir.EhFonteWebPrimaria(KlinikosInstancia.ProvedorConde).Should().BeTrue();
        KlinikosWebInstanciaFhir.EhFonteWebPrimaria(KlinikosInstancia.ProvedorUpa).Should().BeFalse();
        KlinikosWebInstanciaFhir.De(KlinikosInstancia.ProvedorUpa).Slug.Should().Be("upa24h-marica-sqlserver");
    }
}

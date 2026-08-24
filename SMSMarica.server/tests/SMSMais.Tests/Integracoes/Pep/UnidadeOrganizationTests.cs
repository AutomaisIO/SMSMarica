using SMSMarica.Core.Integracoes.Pep.Estrategias.Salux;

namespace SMSMais.Tests.Integracoes.Pep;

/// <summary>
/// Unidade de saúde → Organization (ADR-0039): a unidade é o eixo DURÁVEL do dado clínico,
/// o PEP é proveniência transitória.
///
/// <para>O CNES vem da própria origem (<c>INFOSAUDE.HOSPITAL</c> tem <c>NR_CNES</c> e
/// <c>DS_HOSPITAL</c> nas 3 linhas), então o conector resolve a unidade com zero configuração
/// — nada de de-para mantido à mão, que é onde esse tipo de mapeamento apodrece.</para>
/// </summary>
public class UnidadeOrganizationTests
{
    private static SaluxFhirMapper Mapper() =>
        new("salux-hcml", "https://smsmarica.saude.marica/source/salux/salux-hcml");

    [Fact]
    public void O_SQL_le_as_quatro_colunas_que_importam()
    {
        var sql = SaluxImportacaoStrategy.SqlHospitais();

        Assert.Contains("infosaude.hospital", sql, StringComparison.OrdinalIgnoreCase);
        foreach (var col in new[] { "cd_hospital", "ds_hospital", "nr_cnes", "in_ativo" })
            Assert.Contains(col, sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CNES_entra_como_identificador_NACIONAL()
    {
        var o = Mapper().BuildOrganization(new HospitalLinha(2, "UPA 24H INOÃ", "7164440", "S"));

        var cnes = Assert.Single(o.Identifier, i => i.System == SaluxFhirMapper.IdentCnes);
        Assert.Equal("7164440", cnes.Value);
    }

    [Fact]
    public void Codigo_interno_entra_prefixado_pelo_slug()
    {
        var o = Mapper().BuildOrganization(new HospitalLinha(1, "HOSPITAL MUNICIPAL CONDE MODESTO LEAL", "2266733", "S"));

        var interno = Assert.Single(o.Identifier, i => i.System == SaluxFhirMapper.IdentSaluxHospital);
        Assert.Equal("salux-hcml:1", interno.Value);
        Assert.Equal("HOSPITAL MUNICIPAL CONDE MODESTO LEAL", o.Name);
        Assert.True(o.Active);
    }

    /// <summary>
    /// Unidade sem CNES ainda tem de entrar (pode operar antes do credenciamento) — mas nunca
    /// com identifier vazio, que faz o hub rejeitar o recurso inteiro com 400.
    /// </summary>
    [Fact]
    public void Sem_CNES__entra_so_com_o_codigo_interno_e_sem_identifier_vazio()
    {
        var o = Mapper().BuildOrganization(new HospitalLinha(9, "UNIDADE NOVA", null, "S"));

        Assert.DoesNotContain(o.Identifier, i => string.IsNullOrWhiteSpace(i.Value));
        Assert.DoesNotContain(o.Identifier, i => i.System == SaluxFhirMapper.IdentCnes);
        Assert.Single(o.Identifier);
    }

    [Theory]
    [InlineData("N", false)]
    [InlineData("S", true)]
    [InlineData(null, true)]   // origem omissa não desativa unidade
    public void Ativo_reflete_a_origem(string? ativo, bool esperado) =>
        Assert.Equal(esperado, Mapper().BuildOrganization(new HospitalLinha(1, "X", "123", ativo)).Active);

    [Fact]
    public void CNES_com_mascara_e_normalizado_para_digitos()
    {
        var o = Mapper().BuildOrganization(new HospitalLinha(3, "PA SANTA RITA", " 2.266.792 ", "S"));

        Assert.Equal("2266792", Assert.Single(o.Identifier, i => i.System == SaluxFhirMapper.IdentCnes).Value);
    }

    [Fact]
    public void Encounter_ambulatorial_carrega_a_unidade_onde_aconteceu()
    {
        var b = new BaaLinha(CdPaciente: 1, H: 2, Ano: 2026, Nr: 5,
            DtCheg: "2026-08-01 10:00", DtAtend: null, DtSaida: null,
            Cid: null, CidDs: null, Emerg: "S", RiscoDs: null, Medico: null);

        var semUnidade = Mapper().BuildEncounter(b, "Patient/abc");
        var comUnidade = Mapper().BuildEncounter(b, "Patient/abc", "Organization/xyz");

        Assert.Null(semUnidade.ServiceProvider);
        Assert.Equal("Organization/xyz", comUnidade.ServiceProvider!.Reference);
    }
}

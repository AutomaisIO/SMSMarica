using Automais.Fhir.Core.Organizations;
using FluentAssertions;
using Hl7.Fhir.Model;

namespace Automais.Fhir.Tests;

/// <summary>
/// Identidade da unidade de saúde (ADR-0039). A regra que importa: a MESMA unidade vista por
/// PEPs diferentes tem de convergir para UM recurso — a UPA Inoã é a mesma antes e depois do
/// cutover do Klinikos (abr/2025). O CNES é a ponte entre os códigos internos de cada base.
///
/// <para>Estes testes cobrem a lógica pura de identidade; o upsert contra banco tem cobertura
/// própria em <c>UpsertPorIdentifierTests</c> (Testcontainers).</para>
/// </summary>
public class OrganizationIdentidadeTests
{
    private const string SysCnes = "https://fhir.saude.gov.br/sid/cnes";

    private static Organization Unidade(params Identifier[] ids) =>
        new() { Name = "UPA 24H INOÃ", Identifier = [.. ids] };

    [Fact]
    public void CNES_e_lido_dos_identifiers()
    {
        var o = Unidade(
            new Identifier("urn:salux:hospital", "salux-hcml:2"),
            new Identifier(SysCnes, "7164440"));

        OrganizationService.CnesDe(o).Should().Be("7164440");
    }

    [Fact]
    public void Sem_CNES_declarado_devolve_nulo__nao_explode()
    {
        OrganizationService.CnesDe(Unidade(new Identifier("urn:klinikos:unidade", "upa24h:1")))
            .Should().BeNull();
        OrganizationService.CnesDe(new Organization()).Should().BeNull();
    }

    [Fact]
    public void CNES_com_espaco_e_normalizado()
    {
        OrganizationService.CnesDe(Unidade(new Identifier(SysCnes, "  7164440  ")))
            .Should().Be("7164440");
    }

    /// <summary>
    /// O caso concreto que motivou o ADR: a mesma UPA aparece com nome diferente em cada PEP
    /// ("UPA 24H INOÃ" no Salux, "UPA MARICA" no Klinikos). O nome NÃO pode ser chave — o CNES é.
    /// </summary>
    [Fact]
    public void Nomes_divergentes_entre_PEPs_nao_impedem_o_casamento_pelo_CNES()
    {
        var noSalux = new Organization
        {
            Name = "UPA 24H INOÃ",
            Identifier = [new Identifier(SysCnes, "7164440"), new Identifier("urn:salux:hospital", "salux-hcml:2")],
        };
        var noKlinikos = new Organization
        {
            Name = "UPA MARICA",
            Identifier = [new Identifier(SysCnes, "7164440"), new Identifier("urn:klinikos:unidade", "upa24h-marica:1")],
        };

        OrganizationService.CnesDe(noSalux).Should().Be(OrganizationService.CnesDe(noKlinikos));
        noSalux.Name.Should().NotBe(noKlinikos.Name);
    }

    /// <summary>
    /// O nome da unidade NÃO pode oscilar com quem sincronizou por último. Medido em prod
    /// (04/08): a UPA acumulou 5 versões a mais que as unidades de uma base só, alternando
    /// entre "UPA 24H INOÃ" (Salux) e "UPA MARICA" (Klinikos). Quem nomeou primeiro fica;
    /// o outro nome vira alias — nada se perde, e a identidade para de piscar.
    /// </summary>
    [Fact]
    public void Nome_da_unidade_nao_oscila_entre_conectores__o_outro_vira_alias()
    {
        var noHub = new Organization
        {
            Name = "UPA 24H INOÃ",
            Identifier = [new Identifier(SysCnes, "7164440"), new Identifier("urn:salux:hospital", "salux-hcml:2")],
        };
        var doKlinikos = new Organization
        {
            Name = "UPA MARICA",
            Identifier = [new Identifier(SysCnes, "7164440"), new Identifier("urn:klinikos:unidade", "upa:0006")],
        };

        OrganizationService.EstabilizarNome(doKlinikos, noHub);

        doKlinikos.Name.Should().Be("UPA 24H INOÃ", "quem nomeou primeiro permanece");
        doKlinikos.Alias.Should().Contain("UPA MARICA", "o nome da outra base fica buscável");
    }

    [Fact]
    public void Nome_igual_nao_vira_alias_duplicado()
    {
        var noHub = new Organization { Name = "UPA MARICA" };
        var entrante = new Organization { Name = "UPA MARICA" };

        OrganizationService.EstabilizarNome(entrante, noHub);

        entrante.Name.Should().Be("UPA MARICA");
        entrante.Alias.Should().BeEmpty();
    }

    [Fact]
    public void As_tres_unidades_de_Marica_tem_CNES_distintos()
    {
        var conde = Unidade(new Identifier(SysCnes, "2266733"));
        var inoa = Unidade(new Identifier(SysCnes, "7164440"));
        var staRita = Unidade(new Identifier(SysCnes, "2266792"));

        new[] { conde, inoa, staRita }
            .Select(OrganizationService.CnesDe)
            .Should().OnlyHaveUniqueItems();
    }
}

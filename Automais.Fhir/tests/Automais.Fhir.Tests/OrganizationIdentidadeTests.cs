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

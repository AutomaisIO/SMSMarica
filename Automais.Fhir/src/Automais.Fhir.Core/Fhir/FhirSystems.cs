namespace Automais.Fhir.Core.Fhir;

/// <summary>
/// Systems canônicos de <c>Identifier.system</c> (RNDS / SUS e legados).
/// Centralizados aqui para nunca duplicar string na codebase (ADR-0007 §5, ADR-0009).
/// </summary>
public static class FhirSystems
{
    // RNDS / Ministério da Saúde
    public const string Cpf = "https://fhir.saude.gov.br/sid/cpf";
    public const string Cns = "https://fhir.saude.gov.br/sid/cns";
    public const string Cnes = "https://fhir.saude.gov.br/sid/cnes";

    // Documentos brasileiros (legado / sem system nacional oficial)
    public const string Rg = "urn:br:gov:rg";
    public const string CertidaoNascimento = "urn:br:gov:certidao-nascimento";
    public const string Rne = "urn:br:gov:rne";
    public const string PisPasep = "urn:br:gov:pis-pasep";

    // Conselhos profissionais (sigla + UF concatenadas: urn:br:conselho:crm:RJ)
    public const string ConselhoPrefixRoot = "urn:br:conselho:";
    public const string CrmPrefix = "urn:br:conselho:crm:";
    public const string CorenPrefix = "urn:br:conselho:coren:";

    /// <summary>Monta o system de um registro em conselho: <c>urn:br:conselho:{sigla}:{uf}</c>.</summary>
    public static string ConselhoSystem(string? sigla, string? uf) =>
        ConselhoPrefixRoot
        + (sigla ?? string.Empty).Trim().ToLowerInvariant()
        + ":" + (uf ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Extrai <c>(sigla, uf)</c> de um system <c>urn:br:conselho:{sigla}:{uf}</c>; <c>(null, null)</c> se não casar.</summary>
    public static (string? Sigla, string? Uf) ParseConselho(string? system)
    {
        if (string.IsNullOrEmpty(system) || !system.StartsWith(ConselhoPrefixRoot)) return (null, null);
        var partes = system[ConselhoPrefixRoot.Length..].Split(':', 2);
        var sigla = string.IsNullOrWhiteSpace(partes[0]) ? null : partes[0].ToUpperInvariant();
        var uf = partes.Length > 1 && !string.IsNullOrWhiteSpace(partes[1]) ? partes[1].ToUpperInvariant() : null;
        return (sigla, uf);
    }

    // Prontuários de sistemas-fonte (PEPs)
    public const string SaluxPaciente = "urn:salux:cd_paciente";
    public const string SaluxFia = "urn:salux:fia";
    public const string SaluxBaa = "urn:salux:baa";
    public const string SaluxEdoc = "urn:salux:edoc";

    // Estrutura física do hospital (Location — ADR-0025)
    public const string SaluxUnidade = "urn:salux:unidade";
    public const string SaluxQuarto = "urn:salux:quarto";
    public const string SaluxLeito = "urn:salux:leito";
}

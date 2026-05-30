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

    // Conselhos profissionais (UF concatenada: urn:br:conselho:crm:RJ)
    public const string CrmPrefix = "urn:br:conselho:crm:";
    public const string CorenPrefix = "urn:br:conselho:coren:";

    // Prontuários de sistemas-fonte (PEPs)
    public const string SaluxPaciente = "urn:salux:cd_paciente";
    public const string SaluxFia = "urn:salux:fia";
    public const string SaluxBaa = "urn:salux:baa";
}

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

    // Prime Saúde (Eco Sistemas). O relatório *Pacientes Atendidos*, que é a via de ingestão do
    // histórico, NÃO traz o id do atendimento nem o do paciente — só CNS, profissional e o
    // fechamento do registro. Por isso o identifier do Encounter é uma CHAVE SINTÉTICA derivada
    // de (unidade, fechamento, profissional, paciente) + ordinal, estável entre importações.
    // Quando o atendimento vier pela extensão (que vê o `atendimentoId` real na tela do PEP),
    // usar `PrimeAtendimentoId` — os dois convivem como identifiers do mesmo Encounter.
    // Chave do paciente no Prime (o `Código` da grade de busca — um GUID). O relatório de
    // atendidos NÃO traz, mas a busca de paciente sim, e sem ela um Patient vindo do Prime ficaria
    // sem chave de origem: medido em 23/09/2026, Salux e Klinikos têm chave de origem em 100% dos
    // pacientes, enquanto SISREG/implantação tem em 0% — e é por isso que reconciliar aquela carga
    // depende de adivinhar por CPF/CNS em vez de simplesmente reler a chave.
    public const string PrimePaciente = "urn:prime:paciente";
    public const string PrimeAtendimento = "urn:prime:atendimento";
    public const string PrimeAtendimentoId = "urn:prime:atendimento-id";

    // Estrutura física do hospital (Location — ADR-0025)
    public const string SaluxUnidade = "urn:salux:unidade";
    public const string SaluxQuarto = "urn:salux:quarto";
    public const string SaluxLeito = "urn:salux:leito";
}

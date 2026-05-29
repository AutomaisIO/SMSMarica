using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Patient.identifier[]</c>. CPF, CNS, RG, certidão de nascimento,
/// passaporte, RNE, PIS/PASEP, prontuários externos (SGH, CEM, Salux…).
/// O par (System, Value) é único por paciente.
/// </summary>
public sealed class PatientIdentifier
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>FHIR Identifier.system. URI estável (ex.: https://fhir.saude.gov.br/sid/cpf).</summary>
    public string System { get; set; } = string.Empty;

    /// <summary>FHIR Identifier.value. Valor cru, sem máscara (CPF 11 dígitos).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>FHIR Identifier.type (codificação semântica).</summary>
    public IdentifierTypeCode Type { get; set; } = IdentifierTypeCode.Other;

    /// <summary>FHIR Identifier.use.</summary>
    public IdentifierUse Use { get; set; } = IdentifierUse.Usual;

    /// <summary>FHIR Identifier.period.start.</summary>
    public DateOnly? PeriodStart { get; set; }

    /// <summary>FHIR Identifier.period.end.</summary>
    public DateOnly? PeriodEnd { get; set; }

    /// <summary>Órgão emissor (RG: SSP/DETRAN/IFP…).</summary>
    public string? IssuerName { get; set; }

    /// <summary>UF do órgão emissor (RG).</summary>
    public string? IssuerState { get; set; }

    /// <summary>Cartório (certidão de nascimento).</summary>
    public string? RegistryName { get; set; }

    /// <summary>Livro (certidão).</summary>
    public string? RegistryBook { get; set; }

    /// <summary>Folha (certidão).</summary>
    public string? RegistryPage { get; set; }

    /// <summary>Termo (certidão).</summary>
    public string? RegistryTerm { get; set; }

    /// <summary>FHIR Identifier.assigner — Organization que emitiu.</summary>
    public Guid? AssignerOrganizationId { get; set; }
    public Organization? AssignerOrganization { get; set; }
}

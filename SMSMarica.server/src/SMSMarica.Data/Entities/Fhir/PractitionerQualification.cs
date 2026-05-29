namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Practitioner.qualification[]</c>. CRM, COREN, CRO, CRF
/// (com número + UF + especialidade quando aplicável).
/// </summary>
public sealed class PractitionerQualification
{
    public Guid Id { get; set; }

    public Guid PractitionerId { get; set; }
    public Practitioner Practitioner { get; set; } = null!;

    /// <summary>Tipo de conselho (CRM, COREN, CRO, CRF, RMS…).</summary>
    public string CouncilCode { get; set; } = string.Empty;

    /// <summary>Número do registro.</summary>
    public string CouncilNumber { get; set; } = string.Empty;

    /// <summary>UF do conselho.</summary>
    public string CouncilState { get; set; } = string.Empty;

    /// <summary>Especialidade clínica (CBO Saúde — ex.: 225125 Clínico geral).</summary>
    public string? SpecialtyCode { get; set; }
    public string? SpecialtyName { get; set; }

    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }

    /// <summary>FHIR Practitioner.qualification.issuer.</summary>
    public Guid? IssuerOrganizationId { get; set; }
    public Organization? IssuerOrganization { get; set; }
}

using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Practitioner</c>. Profissional de saúde (médico, enfermeiro,
/// técnico). Identificação + qualificações (CRM, COREN, CRO, CRF, CNS).
/// </summary>
public sealed class Practitioner
{
    public Guid Id { get; set; }

    public int VersionId { get; set; } = 1;
    public DateTime LastUpdated { get; set; }

    public bool Active { get; set; } = true;

    public AdministrativeGender Gender { get; set; } = AdministrativeGender.Unknown;
    public DateOnly? BirthDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public ICollection<PractitionerIdentifier> Identifiers { get; set; } = [];
    public ICollection<PractitionerName> Names { get; set; } = [];
    public ICollection<PractitionerAddress> Addresses { get; set; } = [];
    public ICollection<PractitionerTelecom> Telecoms { get; set; } = [];
    public ICollection<PractitionerQualification> Qualifications { get; set; } = [];
}

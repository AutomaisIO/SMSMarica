using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Patient.contact[]</c> (BackboneElement) — contato de emergência,
/// responsável legal, cuidador. Pode haver mais de um por paciente.
/// </summary>
public sealed class PatientContact
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>FHIR Patient.contact.relationship (codificado v3-RoleCode).</summary>
    public PatientContactRelationship Relationship { get; set; } = PatientContactRelationship.Other;

    /// <summary>Descrição livre do parentesco quando não codificada.</summary>
    public string? RelationshipText { get; set; }

    // Nome (achatado — geralmente contato tem um nome único, não exige HumanName completo)
    public string Name { get; set; } = string.Empty;

    // Telecom (achatado — geralmente só 1 telefone)
    public string? TelephoneDdd { get; set; }
    public string? TelephoneNumber { get; set; }
    public string? Email { get; set; }

    /// <summary>Sexo administrativo do contato (opcional).</summary>
    public AdministrativeGender Gender { get; set; } = AdministrativeGender.Unknown;

    // Endereço (achatado — quando relevante; senão null)
    public string? AddressLine { get; set; }
    public string? AddressCity { get; set; }
    public string? AddressState { get; set; }
    public string? AddressPostalCode { get; set; }

    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
}

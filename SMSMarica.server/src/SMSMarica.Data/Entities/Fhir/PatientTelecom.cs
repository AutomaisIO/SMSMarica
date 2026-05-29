using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Patient.telecom[]</c> (ContactPoint). Telefones, e-mails, etc.
/// </summary>
public sealed class PatientTelecom
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public ContactPointSystem System { get; set; } = ContactPointSystem.Phone;

    /// <summary>FHIR ContactPoint.value. Telefone com DDD+número ou e-mail.</summary>
    public string Value { get; set; } = string.Empty;

    public ContactPointUse Use { get; set; } = ContactPointUse.Home;

    /// <summary>FHIR ContactPoint.rank. Ordem de preferência (1 = primário).</summary>
    public int? Rank { get; set; }

    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
}

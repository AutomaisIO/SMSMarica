using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>FHIR R4 <c>Practitioner.telecom[]</c>.</summary>
public sealed class PractitionerTelecom
{
    public Guid Id { get; set; }

    public Guid PractitionerId { get; set; }
    public Practitioner Practitioner { get; set; } = null!;

    public ContactPointSystem System { get; set; } = ContactPointSystem.Phone;
    public string Value { get; set; } = string.Empty;
    public ContactPointUse Use { get; set; } = ContactPointUse.Work;
    public int? Rank { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
}

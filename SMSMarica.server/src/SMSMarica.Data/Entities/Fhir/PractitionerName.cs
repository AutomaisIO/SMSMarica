using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>FHIR R4 <c>Practitioner.name[]</c>.</summary>
public sealed class PractitionerName
{
    public Guid Id { get; set; }

    public Guid PractitionerId { get; set; }
    public Practitioner Practitioner { get; set; } = null!;

    public NameUse Use { get; set; } = NameUse.Official;
    public string Text { get; set; } = string.Empty;
    public string? Family { get; set; }
    public string[] Given { get; set; } = [];
    public string[] Prefix { get; set; } = [];
    public string[] Suffix { get; set; } = [];
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
}

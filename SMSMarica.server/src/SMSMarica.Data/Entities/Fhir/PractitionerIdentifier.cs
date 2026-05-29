using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>FHIR R4 <c>Practitioner.identifier[]</c>. CPF, CNS, RG e conselhos profissionais.</summary>
public sealed class PractitionerIdentifier
{
    public Guid Id { get; set; }

    public Guid PractitionerId { get; set; }
    public Practitioner Practitioner { get; set; } = null!;

    public string System { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public IdentifierTypeCode Type { get; set; } = IdentifierTypeCode.Other;
    public IdentifierUse Use { get; set; } = IdentifierUse.Usual;
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public string? IssuerName { get; set; }
    public string? IssuerState { get; set; }
}

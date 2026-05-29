using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>FHIR R4 <c>Organization.identifier[]</c>. CNES, CNPJ, etc.</summary>
public sealed class OrganizationIdentifier
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string System { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public IdentifierTypeCode Type { get; set; } = IdentifierTypeCode.Other;
    public IdentifierUse Use { get; set; } = IdentifierUse.Usual;
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
}

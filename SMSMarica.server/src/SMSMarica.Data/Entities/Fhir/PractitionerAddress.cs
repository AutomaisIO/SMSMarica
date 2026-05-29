using SMSMarica.Data.Entities.Fhir.Enums;
using SMSMarica.Data.Entities.Fhir.Lookups;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>FHIR R4 <c>Practitioner.address[]</c>.</summary>
public sealed class PractitionerAddress
{
    public Guid Id { get; set; }

    public Guid PractitionerId { get; set; }
    public Practitioner Practitioner { get; set; } = null!;

    public AddressUse Use { get; set; } = AddressUse.Home;
    public AddressType Type { get; set; } = AddressType.Both;
    public string? Text { get; set; }
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? District { get; set; }
    public int? MunicipioCodigo { get; set; }
    public MunicipioIbge? Municipio { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "BRA";
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
}

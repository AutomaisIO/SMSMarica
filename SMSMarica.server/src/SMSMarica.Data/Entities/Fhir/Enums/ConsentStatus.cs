namespace SMSMarica.Data.Entities.Fhir.Enums;

/// <summary>FHIR R4 Consent.status.</summary>
public enum ConsentStatus
{
    Draft = 0,
    Proposed = 1,
    Active = 2,
    Rejected = 3,
    Inactive = 4,
    EnteredInError = 5,
}

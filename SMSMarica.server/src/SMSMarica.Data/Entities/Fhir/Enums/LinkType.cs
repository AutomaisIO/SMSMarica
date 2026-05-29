namespace SMSMarica.Data.Entities.Fhir.Enums;

/// <summary>FHIR R4 Patient.link.type.</summary>
public enum LinkType
{
    /// <summary>The patient resource containing this link must no longer be used.
    /// The link points forward to another Patient resource that must be used in lieu of this one.</summary>
    ReplacedBy = 0,
    /// <summary>This Patient was unified from the patient pointed to by this link.</summary>
    Replaces = 1,
    /// <summary>Refer to another resource for a more authoritative source of information.</summary>
    Refer = 2,
    /// <summary>The patient resource containing this link is in use and valid but may
    /// be retrieved for additional information about this patient.</summary>
    Seealso = 3,
}

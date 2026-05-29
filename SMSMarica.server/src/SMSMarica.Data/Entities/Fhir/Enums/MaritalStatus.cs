namespace SMSMarica.Data.Entities.Fhir.Enums;

/// <summary>FHIR v3-MaritalStatus codes (Patient.maritalStatus).</summary>
public enum MaritalStatus
{
    Unknown = 0,
    /// <summary>A — Annulled.</summary>
    Annulled = 1,
    /// <summary>D — Divorced.</summary>
    Divorced = 2,
    /// <summary>I — Interlocutory.</summary>
    Interlocutory = 3,
    /// <summary>L — Legally Separated.</summary>
    LegallySeparated = 4,
    /// <summary>M — Married.</summary>
    Married = 5,
    /// <summary>P — Polygamous.</summary>
    Polygamous = 6,
    /// <summary>S — Never Married.</summary>
    NeverMarried = 7,
    /// <summary>T — Domestic partner (união estável).</summary>
    DomesticPartner = 8,
    /// <summary>U — Unmarried (sem subclassificação).</summary>
    Unmarried = 9,
    /// <summary>W — Widowed.</summary>
    Widowed = 10,
}

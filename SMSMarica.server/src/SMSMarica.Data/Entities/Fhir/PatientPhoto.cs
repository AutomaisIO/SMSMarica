namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Patient.photo[]</c> (Attachment). Pode ter várias; uma marcada como principal.
/// Flag <c>AuthorizedDisplay</c> é específica BR (LGPD).
/// </summary>
public sealed class PatientPhoto
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>FHIR Attachment.contentType (image/jpeg, image/png).</summary>
    public string ContentType { get; set; } = "image/jpeg";

    /// <summary>FHIR Attachment.data (base64) OU Attachment.url.</summary>
    public string? DataBase64 { get; set; }
    public string? Url { get; set; }

    /// <summary>FHIR Attachment.title.</summary>
    public string? Title { get; set; }

    /// <summary>FHIR Attachment.creation.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Marca como foto principal (uma por paciente).</summary>
    public bool IsPrimary { get; set; }

    /// <summary>LGPD: autorização explícita pra exibir em painéis/etiquetas.</summary>
    public bool AuthorizedDisplay { get; set; }
}

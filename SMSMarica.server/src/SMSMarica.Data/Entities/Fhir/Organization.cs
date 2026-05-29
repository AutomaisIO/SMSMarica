namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Organization</c>. Hospital, unidade de saúde, Secretaria.
/// Identificador principal: CNES (system: https://fhir.saude.gov.br/sid/cnes).
/// </summary>
public sealed class Organization
{
    public Guid Id { get; set; }

    public int VersionId { get; set; } = 1;
    public DateTime LastUpdated { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>FHIR Organization.name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>FHIR Organization.alias[] como texto único separado por '|' (raro multi).</summary>
    public string? Alias { get; set; }

    /// <summary>FHIR Organization.partOf — organização-mãe.</summary>
    public Guid? PartOfId { get; set; }
    public Organization? PartOf { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public ICollection<OrganizationIdentifier> Identifiers { get; set; } = [];
}

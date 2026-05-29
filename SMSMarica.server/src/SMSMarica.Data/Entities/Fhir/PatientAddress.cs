using SMSMarica.Data.Entities.Fhir.Enums;
using SMSMarica.Data.Entities.Fhir.Lookups;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Patient.address[]</c>. Múltiplos endereços com use=home/work/etc
/// e período de validade.
/// </summary>
public sealed class PatientAddress
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public AddressUse Use { get; set; } = AddressUse.Home;

    public AddressType Type { get; set; } = AddressType.Both;

    /// <summary>FHIR Address.text. Endereço como string única (denormalized).</summary>
    public string? Text { get; set; }

    /// <summary>FHIR Address.line[0]. Logradouro + número.</summary>
    public string? Line1 { get; set; }

    /// <summary>FHIR Address.line[1]. Complemento.</summary>
    public string? Line2 { get; set; }

    /// <summary>FHIR Address.district. Bairro no contexto BR.</summary>
    public string? District { get; set; }

    /// <summary>Município IBGE (FK). FHIR Address.city é derivado do Nome.</summary>
    public int? MunicipioCodigo { get; set; }
    public MunicipioIbge? Municipio { get; set; }

    /// <summary>FHIR Address.state. UF (BR — 2 letras). Derivado do município mas duplicado pra busca rápida.</summary>
    public string? State { get; set; }

    /// <summary>FHIR Address.postalCode. CEP só dígitos.</summary>
    public string? PostalCode { get; set; }

    /// <summary>FHIR Address.country (ISO alfa-3, default BRA).</summary>
    public string Country { get; set; } = "BRA";

    /// <summary>Extensão BR: ponto de referência.</summary>
    public string? ReferencePoint { get; set; }

    /// <summary>Extensão FHIR geolocation: latitude.</summary>
    public decimal? Latitude { get; set; }

    /// <summary>Extensão FHIR geolocation: longitude.</summary>
    public decimal? Longitude { get; set; }

    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
}

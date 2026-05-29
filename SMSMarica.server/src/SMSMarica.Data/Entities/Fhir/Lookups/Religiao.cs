namespace SMSMarica.Data.Entities.Fhir.Lookups;

/// <summary>
/// Religião (catálogo DataSUS). Referenciada por Patient.religion_id
/// (extensão patient-religion).
/// </summary>
public sealed class Religiao
{
    public int Codigo { get; set; }
    public string Nome { get; set; } = string.Empty;
}

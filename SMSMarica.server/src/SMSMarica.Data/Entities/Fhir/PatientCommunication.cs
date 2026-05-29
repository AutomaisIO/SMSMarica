using SMSMarica.Data.Entities.Fhir.Lookups;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Patient.communication[]</c>. Idiomas falados + preferido.
/// Inclui extensão BR pra barreira de comunicação (surdez/cegueira/etc).
/// </summary>
public sealed class PatientCommunication
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>BCP-47 (ex.: pt-BR, en-US, pt-BR-Libras).</summary>
    public string LanguageCode { get; set; } = "pt-BR";

    /// <summary>FHIR Patient.communication.preferred.</summary>
    public bool Preferred { get; set; }

    /// <summary>Extensão BR: barreira de comunicação (DataSUS).</summary>
    public int? BarreiraComunicacaoCodigo { get; set; }
    public BarreiraComunicacao? BarreiraComunicacao { get; set; }
}

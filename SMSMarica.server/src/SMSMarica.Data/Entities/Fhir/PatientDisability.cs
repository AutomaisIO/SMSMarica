using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// Extensão BR (eSUS APS) <c>patient-disability</c>. Múltiplas deficiências
/// possíveis por paciente.
/// </summary>
public sealed class PatientDisability
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public DisabilityType Type { get; set; } = DisabilityType.Outro;

    /// <summary>Descrição livre.</summary>
    public string? Description { get; set; }

    /// <summary>CID-10 quando codificada (ex.: H90.3, F71).</summary>
    public string? CidCode { get; set; }

    public DateOnly? StartDate { get; set; }
}

using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Patient.link[]</c>. Fusão de cadastros duplicados:
/// <c>replaced-by</c> (este foi substituído) / <c>replaces</c> (este substitui outro) /
/// <c>refer</c> / <c>seealso</c>. Equivalente ao <c>CD_PACIENTE_UNIFICADO</c> do Salux.
/// </summary>
public sealed class PatientLink
{
    public Guid Id { get; set; }

    /// <summary>Patient dono do link.</summary>
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>Outro Patient apontado pelo link.</summary>
    public Guid OtherPatientId { get; set; }
    public Patient OtherPatient { get; set; } = null!;

    public LinkType Type { get; set; }

    /// <summary>Motivo da fusão (livre).</summary>
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}

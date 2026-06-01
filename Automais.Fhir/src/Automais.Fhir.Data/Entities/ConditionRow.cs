namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Armazenamento do recurso FHIR <c>Condition</c> (diagnóstico — CID do BAA/FIA).
/// Recurso completo em <see cref="ResourceRow.Content"/> (jsonb).
/// </summary>
public sealed class ConditionRow : ResourceRow
{
    /// <summary>Id do paciente referenciado (Condition.subject = Patient/{id}).</summary>
    public Guid? PatientId { get; set; }

    /// <summary>Id do atendimento referenciado (Condition.encounter = Encounter/{id}).</summary>
    public Guid? EncounterId { get; set; }

    /// <summary>Código do diagnóstico (CID-10).</summary>
    public string? Code { get; set; }
}

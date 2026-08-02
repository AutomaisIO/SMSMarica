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

    /// <summary>System do identifier de negócio (urn:salux:*) — habilita o conditional update.</summary>
    public string? IdentifierSystem { get; set; }

    /// <summary>Valor do identifier de negócio (prefixado pelo slug da base).</summary>
    public string? IdentifierValue { get; set; }
}

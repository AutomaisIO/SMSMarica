namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Armazenamento do recurso FHIR <c>MedicationRequest</c> (item de prescrição —
/// medicação do Salux, PRESC_BAA_OPC_PROD ⋈ MATMED). Recurso completo em
/// <see cref="ResourceRow.Content"/> (jsonb); colunas abaixo são search params.
/// </summary>
public sealed class MedicationRequestRow : ResourceRow
{
    /// <summary>Paciente referenciado (MedicationRequest.subject = Patient/{id}).</summary>
    public Guid? PatientId { get; set; }

    /// <summary>Atendimento referenciado (MedicationRequest.encounter = Encounter/{id}).</summary>
    public Guid? EncounterId { get; set; }

    /// <summary>Texto do medicamento (MedicationRequest.medication.text — ds_material).</summary>
    public string? Medicamento { get; set; }

    /// <summary>Data da prescrição (MedicationRequest.authoredOn) — para ordenar.</summary>
    public DateTimeOffset? AuthoredOn { get; set; }

    /// <summary>System do identifier de negócio (urn:salux:*) — habilita o conditional update.</summary>
    public string? IdentifierSystem { get; set; }

    /// <summary>Valor do identifier de negócio (prefixado pelo slug da base).</summary>
    public string? IdentifierValue { get; set; }
}

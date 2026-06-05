namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Armazenamento do recurso FHIR <c>MedicationAdministration</c> (administração de
/// medicamento — APRAZAMENTO do Salux: o que foi efetivamente administrado, com
/// hora/dose/via). Recurso completo em <see cref="ResourceRow.Content"/> (jsonb);
/// colunas abaixo são search params.
/// </summary>
public sealed class MedicationAdministrationRow : ResourceRow
{
    /// <summary>Paciente referenciado (MedicationAdministration.subject = Patient/{id}).</summary>
    public Guid? PatientId { get; set; }

    /// <summary>Atendimento referenciado (MedicationAdministration.context = Encounter/{id}).</summary>
    public Guid? EncounterId { get; set; }

    /// <summary>Texto do medicamento (medication.text — ds_material).</summary>
    public string? Medicamento { get; set; }

    /// <summary>Momento da administração (effective[x]) — para ordenar.</summary>
    public DateTimeOffset? Effective { get; set; }
}

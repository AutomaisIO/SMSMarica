namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Armazenamento do recurso FHIR <c>Observation</c> (sinais vitais e classificação de
/// risco — origem Salux: <c>SINAIS_VITAIS</c> da triagem + <c>BAA.CD_CLASSIFICACAO_RISCO</c>).
/// Recurso completo em <see cref="ResourceRow.Content"/> (jsonb); colunas abaixo são search params.
/// </summary>
public sealed class ObservationRow : ResourceRow
{
    /// <summary>Paciente referenciado (Observation.subject = Patient/{id}).</summary>
    public Guid? PatientId { get; set; }

    /// <summary>Atendimento referenciado (Observation.encounter = Encounter/{id}).</summary>
    public Guid? EncounterId { get; set; }

    /// <summary>Código LOINC (Observation.code.coding[0].code) — ex.: 8867-4 (FC), 85354-9 (PA).</summary>
    public string? Code { get; set; }

    /// <summary>Momento da aferição (effective[x]) — para ordenar.</summary>
    public DateTimeOffset? Effective { get; set; }

    /// <summary>System do identifier de negócio (urn:salux:*) — habilita o conditional update.</summary>
    public string? IdentifierSystem { get; set; }

    /// <summary>Valor do identifier de negócio (prefixado pelo slug da base).</summary>
    public string? IdentifierValue { get; set; }
}

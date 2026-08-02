namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Armazenamento do recurso FHIR <c>Encounter</c> (atendimento — BAA ambulatorial/
/// urgência ou FIA internação do Salux). Recurso completo em
/// <see cref="ResourceRow.Content"/> (jsonb); colunas abaixo são search params.
/// </summary>
public sealed class EncounterRow : ResourceRow
{
    /// <summary>Id do paciente referenciado (Encounter.subject = Patient/{id}).</summary>
    public Guid? PatientId { get; set; }

    /// <summary>Status FHIR (finished, in-progress…).</summary>
    public string? Status { get; set; }

    /// <summary>Classe do atendimento (AMB, EMER, IMP).</summary>
    public string? Classe { get; set; }

    /// <summary>Início do período (Encounter.period.start) — usado para ordenar a timeline.</summary>
    public DateTimeOffset? PeriodStart { get; set; }

    /// <summary>Fim do período (Encounter.period.end) — "altas do período" nos recortes de internação.</summary>
    public DateTimeOffset? PeriodEnd { get; set; }

    /// <summary>System do identifier de negócio (ex.: urn:salux:baa, urn:salux:fia) — habilita o conditional update.</summary>
    public string? IdentifierSystem { get; set; }

    /// <summary>Valor do identifier de negócio (prefixado pelo slug da base).</summary>
    public string? IdentifierValue { get; set; }
}

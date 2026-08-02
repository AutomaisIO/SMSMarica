namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Armazenamento do recurso FHIR <c>DocumentReference</c> (documento clínico —
/// EDOC do Salux, remontado como HTML). Recurso completo (com o conteúdo inline
/// em content.attachment) vive em <see cref="ResourceRow.Content"/> (jsonb).
/// </summary>
public sealed class DocumentReferenceRow : ResourceRow
{
    /// <summary>Paciente referenciado (DocumentReference.subject = Patient/{id}).</summary>
    public Guid? PatientId { get; set; }

    /// <summary>Atendimento referenciado (DocumentReference.context.encounter = Encounter/{id}).</summary>
    public Guid? EncounterId { get; set; }

    /// <summary>Tipo do documento (DocumentReference.type.text — ex.: nome do modelo EDOC).</summary>
    public string? Tipo { get; set; }

    /// <summary>Data do documento (DocumentReference.date) — para ordenar.</summary>
    public DateTimeOffset? Data { get; set; }

    /// <summary>System do identifier de negócio (urn:salux:*) — habilita o conditional update.</summary>
    public string? IdentifierSystem { get; set; }

    /// <summary>Valor do identifier de negócio (prefixado pelo slug da base).</summary>
    public string? IdentifierValue { get; set; }
}

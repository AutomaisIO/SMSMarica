namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Linha base de armazenamento de um recurso FHIR no modelo document-store.
/// O recurso inteiro vive em <see cref="Content"/> (coluna jsonb); as demais
/// propriedades são metadados de versionamento/auditoria/proveniência comuns a
/// todos os tipos. Cada tipo concreto (Patient, Practitioner, ...) herda e
/// adiciona apenas as colunas de busca (search params) que indexa.
/// </summary>
public abstract class ResourceRow
{
    /// <summary>Id lógico do recurso FHIR (Resource.id).</summary>
    public Guid Id { get; set; }

    /// <summary>Versão do recurso (Meta.versionId); incrementa a cada PUT.</summary>
    public int VersionId { get; set; }

    /// <summary>Última atualização (Meta.lastUpdated).</summary>
    public DateTimeOffset LastUpdated { get; set; }

    /// <summary>
    /// Sistema de origem do recurso (Meta.source) — obrigatório por ADR-0009.
    /// Ex.: https://smsmarica.saude.marica/source/salux
    /// </summary>
    public string MetaSource { get; set; } = null!;

    /// <summary>Exclusão lógica (FHIR DELETE marca como deleted, não apaga).</summary>
    public bool IsDeleted { get; set; }

    /// <summary>O recurso FHIR completo, serializado como JSON (coluna jsonb).</summary>
    public string Content { get; set; } = "{}";
}

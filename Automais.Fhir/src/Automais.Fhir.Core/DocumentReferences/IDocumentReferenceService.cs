using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.DocumentReferences;

/// <summary>Filtros de busca de DocumentReference.</summary>
public sealed record DocumentReferenceBusca(Guid? PatientId = null, Guid? EncounterId = null,
    string? IdentifierSystem = null,
    string? IdentifierValue = null);

/// <summary>Operações sobre o recurso FHIR <c>DocumentReference</c> (documento clínico).</summary>
public interface IDocumentReferenceService
{
    Task<DocumentReference> CriarAsync(DocumentReference doc, CancellationToken ct = default);
    Task<DocumentReference> LerAsync(Guid id, CancellationToken ct = default);
    Task<DocumentReference> AtualizarAsync(Guid id, DocumentReference doc, CancellationToken ct = default);
    /// <summary>
    /// Conditional update (ADR-0024): cria se não existe linha viva com o identifier, senão
    /// atualiza a existente preservando o id lógico. Chave da idempotência do importador.
    /// </summary>
    Task<DocumentReference> UpsertPorIdentifierAsync(string system, string value, DocumentReference recurso, CancellationToken ct = default);

    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(DocumentReferenceBusca filtro, CancellationToken ct = default);
}

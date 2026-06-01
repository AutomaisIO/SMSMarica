using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.DocumentReferences;

/// <summary>Filtros de busca de DocumentReference.</summary>
public sealed record DocumentReferenceBusca(Guid? PatientId = null, Guid? EncounterId = null);

/// <summary>Operações sobre o recurso FHIR <c>DocumentReference</c> (documento clínico).</summary>
public interface IDocumentReferenceService
{
    Task<DocumentReference> CriarAsync(DocumentReference doc, CancellationToken ct = default);
    Task<DocumentReference> LerAsync(Guid id, CancellationToken ct = default);
    Task<DocumentReference> AtualizarAsync(Guid id, DocumentReference doc, CancellationToken ct = default);
    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(DocumentReferenceBusca filtro, CancellationToken ct = default);
}

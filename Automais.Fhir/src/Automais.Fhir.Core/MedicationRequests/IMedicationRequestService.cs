using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.MedicationRequests;

/// <summary>Filtros de busca de MedicationRequest.</summary>
public sealed record MedicationRequestBusca(Guid? PatientId = null, Guid? EncounterId = null,
    string? IdentifierSystem = null,
    string? IdentifierValue = null);

/// <summary>Operações sobre o recurso FHIR <c>MedicationRequest</c> (item de prescrição).</summary>
public interface IMedicationRequestService
{
    Task<MedicationRequest> CriarAsync(MedicationRequest mr, CancellationToken ct = default);
    Task<MedicationRequest> LerAsync(Guid id, CancellationToken ct = default);
    Task<MedicationRequest> AtualizarAsync(Guid id, MedicationRequest mr, CancellationToken ct = default);
    /// <summary>
    /// Conditional update (ADR-0024): cria se não existe linha viva com o identifier, senão
    /// atualiza a existente preservando o id lógico. Chave da idempotência do importador.
    /// </summary>
    Task<MedicationRequest> UpsertPorIdentifierAsync(string system, string value, MedicationRequest recurso, CancellationToken ct = default);

    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(MedicationRequestBusca filtro, CancellationToken ct = default);
}

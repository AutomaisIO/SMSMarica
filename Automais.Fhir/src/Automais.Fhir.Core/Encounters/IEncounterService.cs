using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.Encounters;

/// <summary>Filtros de busca de Encounter.</summary>
public sealed record EncounterBusca(Guid? PatientId = null, string? Status = null);

/// <summary>Operações sobre o recurso FHIR <c>Encounter</c> (atendimento).</summary>
public interface IEncounterService
{
    Task<Encounter> CriarAsync(Encounter encounter, CancellationToken ct = default);
    Task<Encounter> LerAsync(Guid id, CancellationToken ct = default);
    Task<Encounter> AtualizarAsync(Guid id, Encounter encounter, CancellationToken ct = default);
    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(EncounterBusca filtro, CancellationToken ct = default);
}

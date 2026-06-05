using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.Observations;

/// <summary>Filtros de busca de Observation.</summary>
public sealed record ObservationBusca(Guid? PatientId = null, Guid? EncounterId = null, string? Code = null);

/// <summary>Operações sobre o recurso FHIR <c>Observation</c> (sinais vitais, classificação de risco).</summary>
public interface IObservationService
{
    Task<Observation> CriarAsync(Observation o, CancellationToken ct = default);
    Task<Observation> LerAsync(Guid id, CancellationToken ct = default);
    Task<Observation> AtualizarAsync(Guid id, Observation o, CancellationToken ct = default);
    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(ObservationBusca filtro, CancellationToken ct = default);
}

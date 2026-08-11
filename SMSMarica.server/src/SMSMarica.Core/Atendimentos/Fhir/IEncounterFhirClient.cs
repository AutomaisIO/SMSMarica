using Hl7.Fhir.Model;

namespace SMSMarica.Core.Atendimentos.Fhir;

/// <summary>Cliente do hub FHIR para Encounter/Condition (histórico clínico do paciente).</summary>
public interface IEncounterFhirClient
{
    Task<Bundle> BuscarEncountersAsync(Guid pacienteId, CancellationToken ct = default);
    Task<Bundle> BuscarConditionsAsync(Guid pacienteId, CancellationToken ct = default);
    Task<Bundle> BuscarMedicationRequestsAsync(Guid pacienteId, CancellationToken ct = default);
    Task<Bundle> BuscarDocumentsAsync(Guid pacienteId, CancellationToken ct = default);
    Task<Bundle> BuscarObservationsAsync(Guid pacienteId, CancellationToken ct = default);

    /// <summary>
    /// Todas as Organizations do hub — é o catálogo de unidades, não uma busca por paciente.
    /// São poucas (3 em 11/08/2026) e mudam quando entra unidade nova, então cabe inteiro em
    /// memória; resolver o <c>serviceProvider</c> de cada Encounter por GET individual seria
    /// uma requisição por atendimento.
    /// </summary>
    Task<Bundle> BuscarOrganizacoesAsync(CancellationToken ct = default);
}

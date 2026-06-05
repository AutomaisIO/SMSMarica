using Hl7.Fhir.Model;

namespace SMSMarica.Core.Atendimentos.Fhir;

/// <summary>Cliente do hub FHIR para Encounter/Condition (histórico clínico do paciente).</summary>
public interface IEncounterFhirClient
{
    Task<Bundle> BuscarEncountersAsync(Guid pacienteId, CancellationToken ct = default);
    Task<Bundle> BuscarConditionsAsync(Guid pacienteId, CancellationToken ct = default);
    Task<Bundle> BuscarMedicationRequestsAsync(Guid pacienteId, CancellationToken ct = default);
    Task<Bundle> BuscarDocumentsAsync(Guid pacienteId, CancellationToken ct = default);
}

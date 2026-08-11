using Hl7.Fhir.Model;
using SMSMarica.Core.Pacientes.Fhir;

namespace SMSMarica.Core.Atendimentos.Fhir;

public sealed class EncounterFhirClient(HttpClient http) : IEncounterFhirClient
{
    public Task<Bundle> BuscarEncountersAsync(Guid pacienteId, CancellationToken ct = default) =>
        BuscarAsync($"fhir/Encounter?patient={pacienteId}", ct);

    public Task<Bundle> BuscarConditionsAsync(Guid pacienteId, CancellationToken ct = default) =>
        BuscarAsync($"fhir/Condition?patient={pacienteId}", ct);

    public Task<Bundle> BuscarMedicationRequestsAsync(Guid pacienteId, CancellationToken ct = default) =>
        BuscarAsync($"fhir/MedicationRequest?patient={pacienteId}", ct);

    public Task<Bundle> BuscarDocumentsAsync(Guid pacienteId, CancellationToken ct = default) =>
        BuscarAsync($"fhir/DocumentReference?patient={pacienteId}", ct);

    public Task<Bundle> BuscarObservationsAsync(Guid pacienteId, CancellationToken ct = default) =>
        BuscarAsync($"fhir/Observation?patient={pacienteId}", ct);

    public Task<Bundle> BuscarOrganizacoesAsync(CancellationToken ct = default) =>
        BuscarAsync("fhir/Organization", ct);

    private async Task<Bundle> BuscarAsync(string url, CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Hub FHIR retornou {(int)resp.StatusCode}: {json}");
        return FhirJson.Parse<Bundle>(json);
    }
}

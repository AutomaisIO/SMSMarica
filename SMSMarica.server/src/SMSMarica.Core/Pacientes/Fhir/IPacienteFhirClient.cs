using Hl7.Fhir.Model;

namespace SMSMarica.Core.Pacientes.Fhir;

/// <summary>
/// Cliente do hub FHIR (Automais.Fhir) para o recurso Patient. O smsmarica é
/// consumidor: paciente vive só no hub (ver ADR-0010 e regra "FHIR é API-only").
/// </summary>
public interface IPacienteFhirClient
{
    Task<Patient> CriarAsync(Patient patient, CancellationToken ct = default);
    Task<Patient?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<Patient> AtualizarAsync(Guid id, Patient patient, CancellationToken ct = default);
    Task ExcluirAsync(Guid id, CancellationToken ct = default);

    /// <summary>Busca por identifier (system|valor) e/ou nome. Devolve o Bundle searchset.</summary>
    Task<Bundle> BuscarAsync(string? identifier = null, string? name = null, CancellationToken ct = default);
}

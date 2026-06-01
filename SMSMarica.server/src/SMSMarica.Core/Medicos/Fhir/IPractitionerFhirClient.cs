using Hl7.Fhir.Model;

namespace SMSMarica.Core.Medicos.Fhir;

/// <summary>Cliente do hub FHIR (Automais.Fhir) para o recurso Practitioner (médico).</summary>
public interface IPractitionerFhirClient
{
    Task<Practitioner> CriarAsync(Practitioner practitioner, CancellationToken ct = default);
    Task<Practitioner?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<Practitioner> AtualizarAsync(Guid id, Practitioner practitioner, CancellationToken ct = default);
    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(
        string? identifier = null,
        string? name = null,
        string? conselho = null,
        string? conselhoNe = null,
        CancellationToken ct = default);
}

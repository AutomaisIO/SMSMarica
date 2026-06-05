using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.MedicationAdministrations;

/// <summary>Filtros de busca de MedicationAdministration.</summary>
public sealed record MedicationAdministrationBusca(Guid? PatientId = null, Guid? EncounterId = null);

/// <summary>Operações sobre o recurso FHIR <c>MedicationAdministration</c> (administração).</summary>
public interface IMedicationAdministrationService
{
    Task<MedicationAdministration> CriarAsync(MedicationAdministration ma, CancellationToken ct = default);
    Task<MedicationAdministration> LerAsync(Guid id, CancellationToken ct = default);
    Task<MedicationAdministration> AtualizarAsync(Guid id, MedicationAdministration ma, CancellationToken ct = default);
    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(MedicationAdministrationBusca filtro, CancellationToken ct = default);
}

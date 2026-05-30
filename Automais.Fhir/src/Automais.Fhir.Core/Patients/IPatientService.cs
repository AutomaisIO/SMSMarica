using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.Patients;

/// <summary>Filtros de busca de Patient (search params suportados).</summary>
public sealed record PatientBusca(string? Cpf = null, string? Cns = null, string? Nome = null);

/// <summary>
/// Operações sobre o recurso FHIR <c>Patient</c>. Entrada/saída são objetos
/// Firely; a persistência (jsonb + search params) fica encapsulada.
/// </summary>
public interface IPatientService
{
    /// <summary>Cria um novo Patient (gera id e Meta). Equivale ao POST FHIR.</summary>
    Task<Patient> CriarAsync(Patient patient, CancellationToken ct = default);

    /// <summary>Lê um Patient pelo id lógico. Lança se não existir/estiver excluído.</summary>
    Task<Patient> LerAsync(Guid id, CancellationToken ct = default);

    /// <summary>Substitui um Patient existente (PUT FHIR). Incrementa a versão.</summary>
    Task<Patient> AtualizarAsync(Guid id, Patient patient, CancellationToken ct = default);

    /// <summary>Exclusão lógica (DELETE FHIR).</summary>
    Task ExcluirAsync(Guid id, CancellationToken ct = default);

    /// <summary>Busca Patients pelos filtros, devolvendo um Bundle searchset.</summary>
    Task<Bundle> BuscarAsync(PatientBusca filtro, CancellationToken ct = default);
}

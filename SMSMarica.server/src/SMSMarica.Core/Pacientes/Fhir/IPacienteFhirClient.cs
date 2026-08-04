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

    /// <summary>Busca por identifier (system|valor), nome e/ou telefone. Devolve o Bundle searchset.</summary>
    Task<Bundle> BuscarAsync(string? identifier = null, string? name = null, string? telecom = null, CancellationToken ct = default);

    /// <summary>
    /// Busca HUMANA unificada (barra de pesquisa): casa nome (contém) OU CPF/CNS por prefixo,
    /// ordenada prefixo-primeiro. <paramref name="limite"/> segue o "itens por página" da tela.
    /// </summary>
    Task<Bundle> BuscarPorTermoAsync(string termo, int limite, CancellationToken ct = default);

    /// <summary>Busca em lote por ids (search param FHIR <c>_id</c>, OR por vírgula). Devolve o Bundle searchset.</summary>
    Task<Bundle> BuscarPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Página keyset (por Id) de Patients vivos, para manutenção/backfill (ADR-0020 R3).</summary>
    Task<Bundle> ListarParaManutencaoAsync(Guid? cursor, int count, CancellationToken ct = default);
}

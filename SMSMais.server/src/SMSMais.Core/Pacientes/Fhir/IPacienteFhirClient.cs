using Hl7.Fhir.Model;

namespace SMSMais.Core.Pacientes.Fhir;

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

    /// <summary>
    /// Funde dois Patients que são a MESMA pessoa (<c>POST /fhir/Patient/{sobrevivente}/$merge?source={absorvido}</c>).
    /// O hub move os identifiers do absorvido para o sobrevivente, cria <c>Patient.link</c>
    /// (replaces/replaced-by), marca o absorvido <c>active=false</c> (não apaga) e reaponta o
    /// clínico de <c>fhir.*</c>. NÃO alcança <c>smsmarica.*</c> — isso é do repontador local.
    /// </summary>
    Task<ResultadoFusaoHub> FundirAsync(Guid sobreviventeId, Guid absorvidoId, CancellationToken ct = default);
}

/// <summary>O que o <c>$merge</c> do hub moveu, para conferência e para a trilha de auditoria.</summary>
public sealed record ResultadoFusaoHub(
    int IdentifiersAbsorvidos,
    int Encounters,
    int Conditions,
    int Observations,
    int MedicationRequests,
    int MedicationAdministrations,
    int DocumentReferences)
{
    public int TotalClinicoRepontado => Encounters + Conditions + Observations
                                        + MedicationRequests + MedicationAdministrations + DocumentReferences;
}

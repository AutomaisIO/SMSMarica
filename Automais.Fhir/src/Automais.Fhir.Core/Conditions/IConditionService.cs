using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.Conditions;

/// <summary>Filtros de busca de Condition.</summary>
public sealed record ConditionBusca(Guid? PatientId = null, Guid? EncounterId = null,
    string? IdentifierSystem = null,
    string? IdentifierValue = null);

/// <summary>Operações sobre o recurso FHIR <c>Condition</c> (diagnóstico/CID).</summary>
public interface IConditionService
{
    Task<Condition> CriarAsync(Condition condition, CancellationToken ct = default);
    Task<Condition> LerAsync(Guid id, CancellationToken ct = default);
    Task<Condition> AtualizarAsync(Guid id, Condition condition, CancellationToken ct = default);
    /// <summary>
    /// Conditional update (ADR-0024): cria se não existe linha viva com o identifier, senão
    /// atualiza a existente preservando o id lógico. Chave da idempotência do importador.
    /// </summary>
    Task<Condition> UpsertPorIdentifierAsync(string system, string value, Condition recurso, CancellationToken ct = default);

    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(ConditionBusca filtro, CancellationToken ct = default);
}

using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.Encounters;

/// <summary>Filtros de busca de Encounter.</summary>
public sealed record EncounterBusca(
    Guid? PatientId = null,
    string? Status = null,
    string? IdentifierSystem = null,
    string? IdentifierValue = null);

/// <summary>Operações sobre o recurso FHIR <c>Encounter</c> (atendimento).</summary>
public interface IEncounterService
{
    Task<Encounter> CriarAsync(Encounter encounter, CancellationToken ct = default);
    Task<Encounter> LerAsync(Guid id, CancellationToken ct = default);

    /// <summary>Atualiza; com <paramref name="versaoEsperada"/> aplica concorrência otimista (409 em versão obsoleta).</summary>
    Task<Encounter> AtualizarAsync(Guid id, Encounter encounter, int? versaoEsperada = null, CancellationToken ct = default);

    /// <summary>
    /// Conditional update (ADR-0024): cria se não existe linha viva com o identifier, senão
    /// atualiza a existente preservando o id lógico. Chave da idempotência do importador.
    /// </summary>
    Task<Encounter> UpsertPorIdentifierAsync(string system, string value, Encounter encounter, CancellationToken ct = default);

    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(EncounterBusca filtro, CancellationToken ct = default);
}

using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.Locations;

/// <summary>Filtros de busca de Location.</summary>
public sealed record LocationBusca(
    string? IdentifierSystem = null,
    string? IdentifierValue = null,
    Guid? PartOfId = null,
    string? PhysicalType = null);

/// <summary>
/// Operações sobre o recurso FHIR <c>Location</c> (setor/quarto/leito — ADR-0025).
/// Cadastro físico apenas; estado operacional do leito não vive no hub.
/// </summary>
public interface ILocationService
{
    Task<Location> CriarAsync(Location location, CancellationToken ct = default);
    Task<Location> LerAsync(Guid id, CancellationToken ct = default);
    Task<Location> AtualizarAsync(Guid id, Location location, CancellationToken ct = default);

    /// <summary>
    /// Conditional update (ADR-0024): cria se não existe linha viva com o identifier, senão
    /// atualiza a existente preservando o id lógico. Chave da idempotência do importador.
    /// </summary>
    Task<Location> UpsertPorIdentifierAsync(string system, string value, Location location, CancellationToken ct = default);

    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(LocationBusca filtro, CancellationToken ct = default);
}

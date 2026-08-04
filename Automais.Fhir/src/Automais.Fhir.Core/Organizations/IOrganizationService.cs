using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.Organizations;

/// <summary>Filtros de busca de Organization.</summary>
public sealed record OrganizationBusca(
    string? IdentifierSystem = null,
    string? IdentifierValue = null,
    string? Cnes = null,
    Guid? PartOfId = null);

/// <summary>
/// Operações sobre o recurso FHIR <c>Organization</c> — a unidade de saúde (ADR-0039).
///
/// <para>A unidade é o eixo DURÁVEL: sobrevive à troca de PEP. O <c>meta.source</c> responde
/// "de qual sistema o dado veio"; a Organization responde "onde aconteceu". São dimensões
/// ortogonais, e misturá-las quebraria a linha do tempo da unidade a cada cutover — foi o que
/// motivou o ADR.</para>
/// </summary>
public interface IOrganizationService
{
    Task<Organization> CriarAsync(Organization org, CancellationToken ct = default);
    Task<Organization> LerAsync(Guid id, CancellationToken ct = default);
    Task<Organization> AtualizarAsync(Guid id, Organization org, CancellationToken ct = default);

    /// <summary>
    /// Conditional update por identifier — a mesma idempotência dos demais recursos. É por
    /// aqui que cada conector "declara" a sua unidade: o primeiro cria, os seguintes atualizam
    /// a MESMA linha, e cada PEP anexa o seu código próprio ao recurso que já existe.
    /// </summary>
    Task<Organization> UpsertPorIdentifierAsync(string system, string value, Organization org, CancellationToken ct = default);

    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(OrganizationBusca filtro, CancellationToken ct = default);
}

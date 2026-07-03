namespace SMSMarica.Core.Conversas;

/// <summary>Vínculo de um agente a uma unidade (item de leitura).</summary>
public sealed record VinculoUnidadeDto(Guid UnidadeId, string UnidadeNome, bool Principal);

/// <summary>Item de definição de vínculo (escrita).</summary>
public sealed record VinculoUnidadeItem(Guid UnidadeId, bool Principal);

/// <summary>Substitui o conjunto de unidades de um agente. No máximo uma marcada como principal.</summary>
public sealed record DefinirVinculosUnidadeRequest(IReadOnlyList<VinculoUnidadeItem> Unidades);

/// <summary>
/// Gerencia o vínculo N:N agente↔unidade (<c>usuario_unidade</c>), base da visibilidade das filas
/// do chat. Um agente pode cobrir várias unidades; a <see cref="VinculoUnidadeDto.Principal"/> é a
/// padrão usada ao iniciar novas conversas.
/// </summary>
public interface IUsuarioUnidadeService
{
    /// <summary>Ids das unidades que o agente cobre (usado pelo hub e pelo filtro de visibilidade).</summary>
    Task<IReadOnlyList<Guid>> ObterUnidadeIdsAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Unidade padrão do agente (a principal, ou a única, ou <c>null</c> se sem vínculo).</summary>
    Task<Guid?> ObterPrincipalIdAsync(Guid usuarioId, CancellationToken ct = default);

    Task<IReadOnlyList<VinculoUnidadeDto>> ObterVinculosAsync(Guid usuarioId, CancellationToken ct = default);

    Task DefinirVinculosAsync(Guid usuarioId, DefinirVinculosUnidadeRequest request, CancellationToken ct = default);
}

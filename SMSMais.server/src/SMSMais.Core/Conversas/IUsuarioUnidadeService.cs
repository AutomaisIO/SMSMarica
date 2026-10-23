namespace SMSMais.Core.Conversas;

/// <summary>Vínculo de um agente a uma unidade (item de leitura).</summary>
public sealed record VinculoUnidadeDto(Guid UnidadeId, string UnidadeNome, bool Principal);

/// <summary>Item de definição de vínculo (escrita).</summary>
public sealed record VinculoUnidadeItem(Guid UnidadeId, bool Principal);

/// <summary>
/// Substitui o conjunto de unidades de um usuário. Exige ao menos uma e exatamente uma marcada como
/// principal — a unidade padrão (lotação), obrigatória para todo usuário do painel.
/// </summary>
public sealed record DefinirVinculosUnidadeRequest(IReadOnlyList<VinculoUnidadeItem> Unidades);

/// <summary>Usuário vinculado a uma unidade (visão reversa unidade→usuários).</summary>
public sealed record UsuarioDaUnidadeDto(Guid UsuarioId, string NomeCompleto, string? Email, bool Ativo, bool Principal);

/// <summary>
/// Gerencia o vínculo N:N usuário↔unidade (<c>usuario_unidade</c>), base do escopo por unidade e
/// das filas do chat. Um usuário pode cobrir várias unidades; a <see cref="VinculoUnidadeDto.Principal"/>
/// é a <b>unidade padrão</b> (lotação) — obrigatória, uma só: é por ela que a pessoa aparece na
/// Comunicação interna e é a unidade das conversas que ela inicia na Central.
/// </summary>
public interface IUsuarioUnidadeService
{
    /// <summary>Ids das unidades que o agente cobre (usado pelo hub e pelo filtro de visibilidade).</summary>
    Task<IReadOnlyList<Guid>> ObterUnidadeIdsAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Unidade padrão do usuário (a marcada como principal), ou <c>null</c> se ainda não tem.</summary>
    Task<Guid?> ObterPrincipalIdAsync(Guid usuarioId, CancellationToken ct = default);

    Task<IReadOnlyList<VinculoUnidadeDto>> ObterVinculosAsync(Guid usuarioId, CancellationToken ct = default);

    Task DefinirVinculosAsync(Guid usuarioId, DefinirVinculosUnidadeRequest request, CancellationToken ct = default);

    /// <summary>Usuários vinculados à unidade (visão reversa, para a tela de Unidades).</summary>
    Task<IReadOnlyList<UsuarioDaUnidadeDto>> ListarUsuariosDaUnidadeAsync(Guid unidadeId, CancellationToken ct = default);

    /// <summary>
    /// Adiciona um usuário à unidade (idempotente). Se ele ainda não tem unidade padrão, esta vira a
    /// padrão; senão entra como vínculo comum.
    /// </summary>
    Task AdicionarUsuarioAsync(Guid unidadeId, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Remove o vínculo do usuário com a unidade (idempotente). Recusa se for a unidade padrão dele —
    /// a troca da padrão é no cadastro do usuário.
    /// </summary>
    Task RemoverUsuarioAsync(Guid unidadeId, Guid usuarioId, CancellationToken ct = default);
}

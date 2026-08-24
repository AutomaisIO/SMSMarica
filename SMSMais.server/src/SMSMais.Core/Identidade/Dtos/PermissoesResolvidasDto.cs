namespace SMSMais.Core.Identidade.Dtos;

/// <summary>
/// Visão completa das permissões de um usuário, separando o que veio dos perfis,
/// o que veio dos overrides individuais, e o resultado final (união).
/// </summary>
public sealed record PermissoesResolvidasDto(
    IReadOnlyList<PermissaoModuloDto> Herdadas,
    IReadOnlyList<PermissaoModuloDto> Overrides,
    IReadOnlyList<PermissaoModuloDto> Resolvidas);

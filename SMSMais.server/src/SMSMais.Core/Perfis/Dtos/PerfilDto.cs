using SMSMais.Core.Identidade.Dtos;

namespace SMSMais.Core.Perfis.Dtos;

public sealed record PerfilDto(
    Guid Id,
    string Nome,
    string? Descricao,
    bool Ativo,
    DateTime CriadoEm,
    IReadOnlyList<PermissaoModuloDto> Permissoes);

public sealed record PerfilListItemDto(
    Guid Id,
    string Nome,
    string? Descricao,
    bool Ativo,
    int Modulos);

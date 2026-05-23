using SMSMarica.Core.Identidade.Dtos;

namespace SMSMarica.Core.Perfis.Dtos;

public sealed record CadastrarPerfilRequest(
    string Nome,
    string? Descricao,
    IReadOnlyList<PermissaoModuloDto> Permissoes);

public sealed record AtualizarPerfilRequest(
    string Nome,
    string? Descricao,
    bool Ativo,
    IReadOnlyList<PermissaoModuloDto> Permissoes);

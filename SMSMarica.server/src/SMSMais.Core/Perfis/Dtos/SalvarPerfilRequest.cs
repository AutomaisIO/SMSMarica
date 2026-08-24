using SMSMais.Core.Identidade.Dtos;

namespace SMSMais.Core.Perfis.Dtos;

public sealed record CadastrarPerfilRequest(
    string Nome,
    string? Descricao,
    IReadOnlyList<PermissaoModuloDto> Permissoes);

public sealed record AtualizarPerfilRequest(
    string Nome,
    string? Descricao,
    bool Ativo,
    IReadOnlyList<PermissaoModuloDto> Permissoes);

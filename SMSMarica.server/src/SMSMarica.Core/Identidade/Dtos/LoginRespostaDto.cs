namespace SMSMarica.Core.Identidade.Dtos;

public sealed record LoginRespostaDto(
    string Token,
    DateTime ExpiraEm,
    UsuarioDto Usuario,
    IReadOnlyList<PermissaoModuloDto> Permissoes);

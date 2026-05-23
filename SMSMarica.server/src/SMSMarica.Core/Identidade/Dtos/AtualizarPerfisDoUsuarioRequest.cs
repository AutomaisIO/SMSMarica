namespace SMSMarica.Core.Identidade.Dtos;

public sealed record AtualizarPerfisDoUsuarioRequest(IReadOnlyList<Guid> PerfilIds);

public sealed record AtualizarOverridesDoUsuarioRequest(IReadOnlyList<PermissaoModuloDto> Overrides);

public sealed record AlterarSenhaRequest(string SenhaNova);

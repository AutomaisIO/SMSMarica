namespace SMSMarica.Core.Identidade.Dtos;

public sealed record AtualizarPerfisDoUsuarioRequest(IReadOnlyList<Guid> PerfilIds);

public sealed record AtualizarOverridesDoUsuarioRequest(IReadOnlyList<PermissaoModuloDto> Overrides);

/// <summary>Usado por admin para definir a senha de outro usuário (não verifica antiga).</summary>
public sealed record AlterarSenhaRequest(string SenhaNova, bool DeveTrocarNoProximoLogin = false);

/// <summary>Usado pelo próprio usuário (verifica senha atual e limpa a flag).</summary>
public sealed record AlterarMinhaSenhaRequest(string SenhaAtual, string SenhaNova);

public sealed record SenhaGeradaDto(string SenhaGerada, bool DeveTrocarNoProximoLogin);

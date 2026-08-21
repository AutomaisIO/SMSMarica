namespace Automais.Zap.Data.Entities;

/// <summary>Operador da tela de gestão. O relay não tem usuário de negócio nenhum.</summary>
public sealed class UsuarioAdmin
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Email { get; set; }

    /// <summary>PBKDF2 no formato <c>iteracoes.salt_b64.hash_b64</c>.</summary>
    public required string SenhaHash { get; set; }

    public required string Nome { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? UltimoAcessoEm { get; set; }
}

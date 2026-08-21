namespace Automais.Zap.Data.Entities;

/// <summary>Operador do painel. O relay não tem usuário de negócio nenhum.</summary>
public sealed class UsuarioAdmin
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Email { get; set; }

    /// <summary>PBKDF2 no formato <c>iteracoes.salt_b64.hash_b64</c>.</summary>
    public required string SenhaHash { get; set; }

    public required string Nome { get; set; }

    /// <summary>
    /// Enxerga todos os tenants e alterna entre eles. Preenchido automaticamente quando o
    /// e-mail é do domínio da casa (<c>Admin:DominioGlobal</c>), e guardado como campo em vez
    /// de recalculado a cada request: assim mudar a configuração do domínio não tira acesso
    /// de quem já entrava, nem tranca o operador semeado, cujo e-mail pode ser de fora.
    /// </summary>
    public bool Global { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? UltimoAcessoEm { get; set; }

    public ICollection<UsuarioTenant> Tenants { get; set; } = [];
}

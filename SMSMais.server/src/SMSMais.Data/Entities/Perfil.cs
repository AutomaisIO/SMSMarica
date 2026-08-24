namespace SMSMais.Data.Entities;

/// <summary>
/// Agregador de permissões. Cada perfil define um conjunto de
/// <see cref="PermissaoPerfil"/>. Um usuário pode estar associado a vários
/// perfis; suas permissões resolvidas são a união das permissões herdadas
/// dos perfis com os overrides individuais do próprio usuário.
/// </summary>
public class Perfil
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }

    public ICollection<PermissaoPerfil> Permissoes { get; set; } = [];
    public ICollection<UsuarioPerfil> UsuariosPerfis { get; set; } = [];
}

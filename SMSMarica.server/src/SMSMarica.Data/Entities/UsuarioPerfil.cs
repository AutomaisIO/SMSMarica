namespace SMSMarica.Data.Entities;

/// <summary>Junção N:N entre <see cref="Usuario"/> e <see cref="Perfil"/>.</summary>
public class UsuarioPerfil
{
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public Guid PerfilId { get; set; }
    public Perfil Perfil { get; set; } = null!;
}

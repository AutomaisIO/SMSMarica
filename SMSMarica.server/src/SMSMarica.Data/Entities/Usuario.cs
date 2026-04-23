using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

public class Usuario
{
    public Guid Id { get; set; }
    public string NomeCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public PerfilUsuario Perfil { get; set; }
    public string SenhaHash { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }
}

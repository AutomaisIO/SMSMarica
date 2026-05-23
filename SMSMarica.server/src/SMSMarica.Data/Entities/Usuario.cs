using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

public class Usuario
{
    public Guid Id { get; set; }
    public string NomeCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Rg { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public Sexo? Sexo { get; set; }
    public TipoPapel? TipoPapel { get; set; }
    public string? Telefone { get; set; }
    public Endereco? Endereco { get; set; }
    public string? FotoBase64 { get; set; }
    public string SenhaHash { get; set; } = string.Empty;
    public bool DeveTrocarSenha { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }

    public ICollection<UsuarioPerfil> UsuariosPerfis { get; set; } = [];
    public ICollection<PermissaoUsuario> PermissoesOverride { get; set; } = [];
}

using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Núcleo de identidade. Toda pessoa autenticável é uma linha aqui.
/// Papel profissional (médico/motorista/paciente) é determinado pela existência
/// de linha 1:1 nas tabelas correspondentes (sem discriminador na tabela usuario).
/// Ver ADR-0006 (supersede o discriminador <c>tipo_papel</c> do ADR-0005).
/// </summary>
public class Usuario
{
    public Guid Id { get; set; }
    public string NomeCompleto { get; set; } = string.Empty;

    /// <summary>
    /// E-mail de login. Opcional: médicos importados (sem e-mail) podem ter
    /// login só pelo CPF. Quando preenchido, é único. Login aceita e-mail OU CPF.
    /// </summary>
    public string? Email { get; set; }
    public string? Cpf { get; set; }
    public string? Rg { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public Sexo? Sexo { get; set; }
    public string? Telefone { get; set; }
    public Endereco? Endereco { get; set; }
    public string? FotoBase64 { get; set; }

    /// <summary>
    /// Preferências de UI do próprio usuário (JSONB), ex.: tela default de cada
    /// seção do menu. Opaco para o domínio — quem dá forma é o front. <c>null</c>
    /// quando o usuário nunca personalizou nada.
    /// </summary>
    public string? PreferenciasUi { get; set; }

    public string SenhaHash { get; set; } = string.Empty;
    public bool DeveTrocarSenha { get; set; }

    /// <summary>
    /// Flag de acesso: <c>false</c> bloqueia login (revogação temporária).
    /// Não é marcador de exclusão — para isso use <see cref="ExcluidoEm"/>.
    /// </summary>
    public bool Ativo { get; set; } = true;

    public DateTime? UltimoAcessoEm { get; set; }

    // Auditoria (criação / edição / exclusão lógica)
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    // RBAC
    public ICollection<UsuarioPerfil> UsuariosPerfis { get; set; } = [];
    public ICollection<PermissaoUsuario> PermissoesOverride { get; set; } = [];

    // Papel operacional 1:1 (Motorista). Paciente e Médico migraram para o hub
    // FHIR (Patient/Practitioner) — Usuário não carrega mais papel clínico.
    public Motorista? Motorista { get; set; }
}

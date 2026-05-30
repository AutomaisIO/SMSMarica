using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Núcleo de identidade reduzido. Toda pessoa autenticável é uma linha aqui.
/// Após o refator FHIR (Fatias 2–4), dados pessoais (nome, CPF, RG, sexo,
/// endereço, telefone, foto) saíram daqui: vivem em <c>fhir.patient</c>
/// (cidadão), <c>fhir.practitioner</c> (médico/enfermeiro/etc.) ou
/// <see cref="Motorista"/> (inline, motorista não é entidade clínica FHIR).
/// O Usuario carrega apenas credenciais, flag de acesso, RBAC e um
/// nome denormalizado para UI quando o papel não está setado (admin/operador).
/// O papel ativo é determinado por qual das três FKs nullable está populada —
/// CHECK constraint garante no máximo uma.
/// </summary>
public class Usuario
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public bool DeveTrocarSenha { get; set; }

    /// <summary>
    /// Flag de acesso: <c>false</c> bloqueia login (revogação temporária).
    /// Não é marcador de exclusão — para isso use <see cref="ExcluidoEm"/>.
    /// </summary>
    public bool Ativo { get; set; } = true;

    public DateTime? UltimoAcessoEm { get; set; }

    /// <summary>
    /// Nome de exibição denormalizado. Para Usuario com papel é sincronizado
    /// a partir do papel (Patient/Practitioner/Motorista) no cadastro/atualização.
    /// Para Usuario sem papel (admin/operador) é editado direto.
    /// </summary>
    public string NomeExibicao { get; set; } = string.Empty;

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

    // Papéis (3 FKs nullable, no máximo 1 setada — CHECK constraint na migration).
    /// <summary>Cidadão — FHIR Patient (cross-schema).</summary>
    public Guid? PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Profissional de saúde — FHIR Practitioner (cross-schema).</summary>
    public Guid? PractitionerId { get; set; }
    public Practitioner? Practitioner { get; set; }

    /// <summary>Motorista (carrega identidade inline — não é entidade FHIR).</summary>
    public Guid? MotoristaId { get; set; }
    public Motorista? Motorista { get; set; }
}

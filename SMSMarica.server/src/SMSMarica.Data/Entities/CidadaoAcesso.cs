namespace SMSMarica.Data.Entities;

/// <summary>
/// Credenciais e identidade federada do <b>cidadão</b> (paciente) no app.
/// NÃO é um <see cref="Usuario"/> (RBAC) — o cidadão não tem papel no sistema.
/// A identidade clínica vive no hub FHIR (<c>fhir.patient</c>); aqui guardamos
/// apenas o que é necessário para autenticar: senha (opcional), vínculos sociais
/// (Google/Microsoft/Facebook) e o controle de sessão única por dispositivo.
///
/// <para><b>Fonte da verdade é o CPF</b>: todo método de login resolve para o
/// mesmo cidadão pelo CPF. <see cref="PatientId"/> é a referência (estável) ao
/// recurso Patient no hub FHIR (serviço autônomo — ADR-0010 — por isso não é FK).</para>
/// </summary>
public class CidadaoAcesso
{
    public Guid Id { get; set; }

    /// <summary>Id do recurso Patient no hub FHIR (referência lógica, sem FK cross-serviço).</summary>
    public Guid PatientId { get; set; }

    /// <summary>CPF (11 dígitos) — fonte da verdade da identidade do cidadão. Único.</summary>
    public string Cpf { get; set; } = string.Empty;

    /// <summary>Hash da senha (PBKDF2 via PasswordHasher). Null = cidadão ainda sem senha (só OTP/social).</summary>
    public string? SenhaHash { get; set; }

    // Vínculos de login social (subject id de cada provedor). Únicos quando preenchidos.
    public string? GoogleSub { get; set; }
    public string? MicrosoftSub { get; set; }
    public string? FacebookSub { get; set; }

    /// <summary>Acesso habilitado. False bloqueia login sem apagar o registro.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>Sessões (histórico). No máximo uma ativa por vez (single-device).</summary>
    public ICollection<CidadaoSessao> Sessoes { get; set; } = [];

    /// <summary>Consentimentos LGPD (histórico). Acesso exige um ativo da versão vigente.</summary>
    public ICollection<CidadaoConsentimento> Consentimentos { get; set; } = [];
}

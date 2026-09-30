namespace SMSMais.Data.Entities;

/// <summary>
/// Autorização VIDaaS em nuvem de um médico que ainda vale para novas assinaturas (ADR-0061 §2.1).
/// O médico aprova UMA vez no aplicativo; a credencial que a IntegraICP devolve serve para vários
/// <c>POST /signatures</c> até o <c>credential_lifetime</c> pedido. Sem esta linha, cada laudo
/// exigia uma aprovação nova no celular.
///
/// <para>
/// <b>Vive e morre com a sessão de login no SMSMais</b> (<see cref="SessaoLoginId"/> = o
/// <c>jti</c> do token): só é usada por requisições desse mesmo login, dura no máximo o que resta
/// dele e é encerrada no logout. Sair e entrar de novo pede nova aprovação no aplicativo.
/// </para>
///
/// <para>
/// Quem autoriza cada assinatura continua sendo o médico logado: a sessão só é usada quando o
/// próprio autor clica em Assinar no painel, e cada documento passa pela conferência. O que a
/// sessão dispensa é só a volta ao aplicativo.
/// </para>
///
/// Keyed pelo id do Practitioner no hub FHIR, sem FK (mesma régua da rubrica e do modo).
/// </summary>
public class SessaoAssinaturaNuvem
{
    public Guid Id { get; set; }

    /// <summary>Id do Practitioner (hub FHIR) dono da autorização.</summary>
    public Guid MedicoId { get; set; }

    /// <summary>Sessão de login no SMSMais (o <c>jti</c> do token) à qual a autorização pertence.</summary>
    public string SessaoLoginId { get; set; } = string.Empty;

    /// <summary>
    /// <c>credentialId</c> devolvido pela IntegraICP no retorno da autorização. Sozinho não
    /// assina nada: a API exige o <see cref="CodeVerifier"/> junto.
    /// </summary>
    public string CredencialId { get; set; } = string.Empty;

    /// <summary>
    /// <c>code_verifier</c> do PKCE, cifrado com Data Protection. Apagado ao encerrar a sessão —
    /// sem ele a credencial não pode mais ser usada, mesmo que ainda esteja viva no provedor.
    /// </summary>
    public string? CodeVerifier { get; set; }

    /// <summary>Thumbprint do certificado da credencial (auditoria).</summary>
    public string? CertThumbprint { get; set; }

    /// <summary>Quando o médico aprovou no aplicativo (retorno da autorização).</summary>
    public DateTime AutorizadaEm { get; set; }

    /// <summary>
    /// Fim da validade: o que restava da sessão de login ao pedir a autorização, limitado ao teto
    /// configurado (é o <c>credential_lifetime</c> enviado ao provedor). O provedor é a fonte da
    /// verdade: se recusar antes, a sessão é encerrada e o médico aprova de novo.
    /// </summary>
    public DateTime ExpiraEm { get; set; }

    /// <summary>Usuário logado que disparou a autorização.</summary>
    public Guid? AutorizadaPorUsuarioId { get; set; }

    /// <summary>Preenchido quando a sessão deixa de valer (expirou, foi recusada, encerrada ou substituída).</summary>
    public DateTime? EncerradaEm { get; set; }

    /// <summary>Por que a sessão acabou (texto curto, para auditoria).</summary>
    public string? MotivoEncerramento { get; set; }

    /// <summary>Usuário que encerrou (logout ou botão do painel); nulo quando o sistema encerrou sozinho.</summary>
    public Guid? EncerradaPorUsuarioId { get; set; }
}

namespace SMSMais.Data.Entities;

/// <summary>
/// Sessão de login do cidadão. Um paciente pode ter várias ao mesmo tempo (app, PWA,
/// navegador): o login não revoga as anteriores. O <see cref="Id"/> vai no JWT como
/// <c>jti</c>; a cada request o token é validado contra a sessão — sessão revogada ou
/// expirada derruba a autenticação.
/// </summary>
public class CidadaoSessao
{
    /// <summary>Id da sessão. Vai como <c>jti</c> no JWT do cidadão.</summary>
    public Guid Id { get; set; }

    public Guid CidadaoAcessoId { get; set; }
    public CidadaoAcesso CidadaoAcesso { get; set; } = null!;

    /// <summary>Como autenticou: <c>otp-whatsapp</c> | <c>senha</c> | <c>google</c> | <c>microsoft</c> | <c>facebook</c>.</summary>
    public string Canal { get; set; } = string.Empty;

    /// <summary>Rótulo do dispositivo (user-agent/modelo), para o cidadão reconhecer a sessão.</summary>
    public string? Dispositivo { get; set; }

    public string? Ip { get; set; }

    public DateTime CriadaEm { get; set; }
    public DateTime ExpiraEm { get; set; }

    /// <summary>Null = sessão ativa. Preenchido = revogada (logout ou login em outro device).</summary>
    public DateTime? RevogadaEm { get; set; }

    /// <summary>
    /// Token do Firebase Cloud Messaging do aparelho desta sessão (push do app do cidadão).
    /// O aparelho pertence à sessão, não ao paciente: logout apaga o token junto, e o envio só
    /// alcança sessão ativa — é o que impede push para quem já saiu do app.
    /// </summary>
    public string? PushToken { get; set; }

    /// <summary><c>android</c> | <c>ios</c>.</summary>
    public string? PushPlataforma { get; set; }

    public DateTime? PushRegistradoEm { get; set; }
}

namespace SMSMais.Data.Entities;

/// <summary>
/// Sessão de login do cidadão. <b>Single-device</b>: ao autenticar (por qualquer
/// método), todas as sessões ativas anteriores são revogadas e uma nova é criada.
/// O <see cref="Id"/> vai no JWT como <c>jti</c>; a cada request o token é validado
/// contra a sessão — se o aparelho antigo mandar o token revogado, a autenticação quebra.
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
}

namespace SMSMarica.Data.Entities.Notificacoes;

/// <summary>
/// Configuração (linha única) da integração WhatsApp (Meta Cloud API) do município.
/// Segredos (token do System User, verify token, app secret) cifrados em repouso
/// (IProtetorSegredos), write-only na API.
/// </summary>
/// <remarks>
/// Antiga <c>TfdConfigWhatsApp</c> / <c>tfd_config_whatsapp</c>. O canal WhatsApp é do
/// município e atende todo o sistema — nunca foi do TFD (ADR-0038).
/// </remarks>
public class WhatsAppConfiguracao
{
    public Guid Id { get; set; }
    public string BaseUrl { get; set; } = "https://graph.facebook.com/v21.0/";

    /// <summary>Token do System User (permanente) cifrado. Write-only.</summary>
    public string? TokenCifrado { get; set; }

    public string? PhoneNumberId { get; set; }
    public string? WabaId { get; set; }

    /// <summary>Verify token do webhook (cifrado).</summary>
    public string? VerifyTokenCifrado { get; set; }

    /// <summary>App Secret p/ validar a assinatura X-Hub-Signature-256 do webhook (cifrado).</summary>
    public string? AppSecretCifrado { get; set; }

    /// <summary>
    /// Base da API do Automais.Zap. Quando <see cref="ZapAtivo"/>, o envio sai por ela em vez
    /// de ir direto ao graph.facebook.com — o token do System User deixa de viver aqui e passa
    /// a existir num lugar só (ADR-0044).
    /// </summary>
    public string? ZapBaseUrl { get; set; }

    /// <summary>Token de tenant emitido pelo Automais.Zap (cifrado). Write-only.</summary>
    public string? ZapTokenCifrado { get; set; }

    /// <summary>
    /// Segredo combinado com o Automais.Zap para conferir o que ELE entrega no nosso webhook
    /// (cifrado). Preenchido, o webhook passa a aceitar X-Automais-Signature.
    ///
    /// Existe porque conferir a assinatura da Meta só funciona enquanto os dois lados usam o
    /// mesmo App Secret — e durante a migração há dois Apps com segredos diferentes.
    /// </summary>
    public string? ZapSegredoWebhookCifrado { get; set; }

    /// <summary>
    /// Desligado, o envio volta a sair direto para a Meta com <see cref="TokenCifrado"/>.
    /// É a saída de emergência: o relay passa a ser caminho crítico do envio, e voltar atrás
    /// tem de ser uma chave, não um deploy.
    /// </summary>
    public bool ZapAtivo { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}

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

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}

namespace SMSMarica.Core.Tfd.Configuracao;

/// <summary>Contexto resolvido (segredos revelados) da Google Maps Platform.</summary>
public sealed record TfdGoogleContexto(string BaseUrl, string ApiKey);

/// <summary>Contexto resolvido (segredos revelados) do WhatsApp Cloud API (Meta).</summary>
public sealed record TfdWhatsAppContexto(
    string BaseUrl,
    string Token,
    string PhoneNumberId,
    string? WabaId,
    string? VerifyToken,
    string? AppSecret,
    string? ZapBaseUrl = null,
    string? ZapToken = null,
    bool ZapAtivo = false,
    string? ZapSegredoWebhook = null)
{
    /// <summary>Envio (e listagem de templates) sai pelo Automais.Zap.</summary>
    public bool ViaZap => ZapAtivo
                          && !string.IsNullOrWhiteSpace(ZapBaseUrl)
                          && !string.IsNullOrWhiteSpace(ZapToken);
}

/// <summary>
/// Configuração (linha única por integração) das credenciais externas do TFD.
/// Segredos cifrados em repouso (write-only). Espelha <c>SisregConfiguracaoService</c>.
/// </summary>
public interface ITfdConfigService
{
    Task<TfdConfigGoogleDto> ObterGoogleAsync(CancellationToken ct = default);
    Task AtualizarGoogleAsync(AtualizarTfdConfigGoogleRequest request, CancellationToken ct = default);
    Task<TfdGoogleContexto> ObterGoogleContextoAsync(CancellationToken ct = default);

    Task<TfdConfigWhatsAppDto> ObterWhatsAppAsync(CancellationToken ct = default);
    Task AtualizarWhatsAppAsync(AtualizarTfdConfigWhatsAppRequest request, CancellationToken ct = default);
    Task<TfdWhatsAppContexto> ObterWhatsAppContextoAsync(CancellationToken ct = default);
}

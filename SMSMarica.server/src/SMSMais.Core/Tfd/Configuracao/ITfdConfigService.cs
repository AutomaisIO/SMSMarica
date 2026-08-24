namespace SMSMais.Core.Tfd.Configuracao;

/// <summary>Contexto resolvido (segredos revelados) da Google Maps Platform.</summary>
public sealed record TfdGoogleContexto(string BaseUrl, string ApiKey);

/// <summary>
/// Contexto resolvido do canal WhatsApp. Não há credencial da Meta aqui: quem fala com ela é
/// o Automais.Zap.
/// </summary>
public sealed record TfdWhatsAppContexto(
    string PhoneNumberId,
    string ZapBaseUrl,
    string ZapToken,
    string? ZapSegredoWebhook);

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

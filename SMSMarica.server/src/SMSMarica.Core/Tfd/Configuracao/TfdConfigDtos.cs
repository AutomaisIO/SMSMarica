namespace SMSMarica.Core.Tfd.Configuracao;

public sealed record TfdConfigGoogleDto(string BaseUrl, bool ChaveConfigurada, bool Ativo);

public sealed record AtualizarTfdConfigGoogleRequest(string BaseUrl, string? ApiKey, bool Ativo);

/// <summary>
/// Canal WhatsApp da instância. Sem campo da Meta: token do System User, App Secret, verify
/// token e WABA vivem no Automais.Zap, que é quem fala com ela (ADR-0044).
/// </summary>
public sealed record TfdConfigWhatsAppDto(
    string? PhoneNumberId,
    string? ZapBaseUrl,
    bool ZapTokenConfigurado,
    bool ZapSegredoWebhookConfigurado,
    bool ZapAtivo,
    bool Ativo);

/// <summary>Segredo em branco mantém o que está gravado — a tela nunca reexibe o valor.</summary>
public sealed record AtualizarTfdConfigWhatsAppRequest(
    string? PhoneNumberId,
    string? ZapBaseUrl,
    string? ZapToken,
    string? ZapSegredoWebhook,
    bool ZapAtivo,
    bool Ativo);

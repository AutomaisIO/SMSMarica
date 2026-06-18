namespace SMSMarica.Core.Tfd.Configuracao;

public sealed record TfdConfigGoogleDto(string BaseUrl, bool ChaveConfigurada, bool Ativo);

public sealed record AtualizarTfdConfigGoogleRequest(string BaseUrl, string? ApiKey, bool Ativo);

public sealed record TfdConfigWhatsAppDto(
    string BaseUrl,
    string? PhoneNumberId,
    string? WabaId,
    bool TokenConfigurado,
    bool VerifyTokenConfigurado,
    bool AppSecretConfigurado,
    bool Ativo);

public sealed record AtualizarTfdConfigWhatsAppRequest(
    string BaseUrl,
    string? Token,
    string? PhoneNumberId,
    string? WabaId,
    string? VerifyToken,
    string? AppSecret,
    bool Ativo);

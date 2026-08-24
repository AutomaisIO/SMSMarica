namespace SMSMais.Api.Auth;

public sealed class JwtOptions
{
    public const string Secao = "Auth:Jwt";

    public string Issuer { get; set; } = "smsmarica";
    public string Audience { get; set; } = "smsmarica";
    public string Key { get; set; } = string.Empty;
    public int ExpiraEmMinutos { get; set; } = 60 * 8;
}

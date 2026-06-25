namespace SMSMarica.Core.Anexos;

/// <summary>
/// Configuração da ponte QR → PWA de anexos de exame (seção <c>Anexos</c>).
/// </summary>
public sealed class AnexosOptions
{
    public const string Secao = "Anexos";

    /// <summary>Base do PWA usada para montar a URL do QR (<c>{PwaBaseUrl}/?t={token}</c>).</summary>
    public string PwaBaseUrl { get; set; } = "https://arquivos.smsmarica.online";

    /// <summary>Tempo de vida do token de upload, em minutos.</summary>
    public int TokenTtlMinutos { get; set; } = 15;
}

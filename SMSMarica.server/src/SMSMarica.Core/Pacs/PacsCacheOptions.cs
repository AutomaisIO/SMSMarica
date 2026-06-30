namespace SMSMarica.Core.Pacs;

/// <summary>
/// Configuração do cache em disco do PACS (seção <c>Pacs:Cache</c>).
/// </summary>
public sealed class PacsCacheOptions
{
    public const string SecaoConfig = "Pacs:Cache";

    /// <summary>Liga/desliga o cache. Default ligado.</summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>
    /// Diretório de persistência. Se null/vazio, usa
    /// <c>Path.Combine(Path.GetTempPath(), "smsmarica-pacs-cache")</c>.
    /// </summary>
    public string? Diretorio { get; set; }

    /// <summary>Teto total do cache em MB (evicção LRU ao exceder). Default 4096.</summary>
    public long TamanhoMaximoMb { get; set; } = 4096;

    /// <summary>Teto por item em MB (acima disto não cacheia). Default 64.</summary>
    public long TamanhoMaximoItemMb { get; set; } = 64;
}

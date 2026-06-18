namespace SMSMarica.Data.Entities.Tfd;

/// <summary>
/// Configuração (linha única) da Google Maps Platform usada pelo TFD (Geocoding,
/// Distance Matrix, Routes). Chave de API cifrada em repouso (IProtetorSegredos),
/// write-only na API. Mesma filosofia de <c>SisregConfiguracao</c>.
/// </summary>
public class TfdConfigGoogle
{
    public Guid Id { get; set; }
    public string BaseUrl { get; set; } = "https://maps.googleapis.com/";

    /// <summary>Chave da API cifrada. Write-only na API.</summary>
    public string? ApiKeyCifrada { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}

namespace SMSMarica.Data.Entities.Geo;

/// <summary>
/// Configuração (linha única) da Google Maps Platform (Geocoding, Distance Matrix,
/// Routes). Chave de API cifrada em repouso (IProtetorSegredos), write-only na API.
/// Mesma filosofia de <c>SisregConfiguracao</c>.
/// </summary>
/// <remarks>
/// Antiga <c>TfdConfigGoogle</c> / <c>tfd_config_google</c>. A chave do Google Maps é
/// do município e serve a qualquer módulo que precise de geo — ADR-0038.
/// </remarks>
public class GeoConfiguracao
{
    public Guid Id { get; set; }
    public string BaseUrl { get; set; } = "https://maps.googleapis.com/";

    /// <summary>Chave da API cifrada. Write-only na API.</summary>
    public string? ApiKeyCifrada { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}

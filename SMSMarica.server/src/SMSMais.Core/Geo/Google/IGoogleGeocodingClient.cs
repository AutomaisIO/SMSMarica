namespace SMSMais.Core.Geo.Google;

public sealed record GoogleGeoResultado(double Latitude, double Longitude, string? Precisao, bool Ok);

public interface IGoogleGeocodingClient
{
    Task<GoogleGeoResultado?> GeocodificarAsync(string endereco, CancellationToken ct = default);
}

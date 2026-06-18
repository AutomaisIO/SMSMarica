using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SMSMarica.Core.Tfd.Configuracao;

namespace SMSMarica.Core.Geo.Google;

/// <summary>
/// Cliente da Geocoding API do Google Maps. A chave e a URL base vêm da configuração
/// cifrada (<see cref="ITfdConfigService"/>), montando a URL absoluta por chamada.
/// </summary>
public sealed class GoogleGeocodingClient(HttpClient http, ITfdConfigService config) : IGoogleGeocodingClient
{
    public async Task<GoogleGeoResultado?> GeocodificarAsync(string endereco, CancellationToken ct = default)
    {
        var ctx = await config.ObterGoogleContextoAsync(ct);
        var baseUrl = ctx.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/maps/api/geocode/json?address={Uri.EscapeDataString(endereco)}&components=country:BR&language=pt-BR&key={ctx.ApiKey}";

        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<GeoResposta>(cancellationToken: ct);
        if (payload is null || payload.Status != "OK" || payload.Results is not { Count: > 0 })
        {
            return new GoogleGeoResultado(0, 0, payload?.Status, false);
        }

        var loc = payload.Results[0].Geometry?.Location;
        if (loc is null) return new GoogleGeoResultado(0, 0, payload.Status, false);

        return new GoogleGeoResultado(loc.Lat, loc.Lng, payload.Results[0].Geometry?.LocationType, true);
    }

    private sealed record GeoResposta(string Status, List<GeoResultado>? Results);
    private sealed record GeoResultado(GeoGeometry? Geometry);
    private sealed record GeoGeometry(GeoLocation? Location, [property: JsonPropertyName("location_type")] string? LocationType);
    private sealed record GeoLocation(double Lat, double Lng);
}

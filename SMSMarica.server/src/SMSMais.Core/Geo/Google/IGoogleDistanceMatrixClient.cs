using System.Net.Http.Json;
using SMSMais.Core.Tfd.Configuracao;

namespace SMSMais.Core.Geo.Google;

public interface IGoogleDistanceMatrixClient
{
    /// <summary>Distância de carro (metros) origem→destino via Distance Matrix. Null se a API não resolver.</summary>
    Task<long?> DistanciaMetrosAsync(Coordenada origem, Coordenada destino, CancellationToken ct = default);
}

public sealed class GoogleDistanceMatrixClient(HttpClient http, ITfdConfigService config) : IGoogleDistanceMatrixClient
{
    public async Task<long?> DistanciaMetrosAsync(Coordenada origem, Coordenada destino, CancellationToken ct = default)
    {
        var ctx = await config.ObterGoogleContextoAsync(ct); // lança se não configurado → caller usa haversine
        var baseUrl = ctx.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/maps/api/distancematrix/json"
            + $"?origins={origem.Latitude},{origem.Longitude}"
            + $"&destinations={destino.Latitude},{destino.Longitude}"
            + $"&mode=driving&key={ctx.ApiKey}";

        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<MatrizResposta>(cancellationToken: ct);

        var el = payload?.Rows?.FirstOrDefault()?.Elements?.FirstOrDefault();
        if (el is null || el.Status != "OK" || el.Distance is null) return null;
        return el.Distance.Value;
    }

    private sealed record MatrizResposta(string Status, List<Linha>? Rows);
    private sealed record Linha(List<Elemento>? Elements);
    private sealed record Elemento(string Status, Valor? Distance, Valor? Duration);
    private sealed record Valor(long Value, string? Text);
}

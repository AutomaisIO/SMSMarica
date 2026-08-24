using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SMSMais.Core.Tfd.Configuracao;

namespace SMSMais.Core.Geo.Google;

/// <summary>Ordem otimizada das coletas + distância/duração de carro (ida).</summary>
public sealed record RotaOtimizada(IReadOnlyList<int> OrdemColetas, long DistanciaMetros, long DuracaoSegundos);

public interface IGoogleRoutesClient
{
    /// <summary>
    /// Otimiza a ordem das coletas (waypoint optimization, driving) terminando no destino.
    /// <c>OrdemColetas</c> são índices na lista <paramref name="coletas"/>. Null se não resolver.
    /// </summary>
    Task<RotaOtimizada?> OtimizarColetaAsync(IReadOnlyList<Coordenada> coletas, Coordenada destino, CancellationToken ct = default);
}

public sealed class GoogleRoutesClient(HttpClient http, ITfdConfigService config) : IGoogleRoutesClient
{
    public async Task<RotaOtimizada?> OtimizarColetaAsync(
        IReadOnlyList<Coordenada> coletas, Coordenada destino, CancellationToken ct = default)
    {
        if (coletas.Count == 0) return null;
        var ctx = await config.ObterGoogleContextoAsync(ct); // lança se não configurado → caller usa heurística
        var baseUrl = ctx.BaseUrl.TrimEnd('/');

        // Coleta o mais distante do destino primeiro (origem); os demais são waypoints otimizáveis.
        var origemIdx = 0;
        var maior = double.MinValue;
        for (var i = 0; i < coletas.Count; i++)
        {
            var d = Haversine(coletas[i], destino);
            if (d > maior) { maior = d; origemIdx = i; }
        }
        var outros = Enumerable.Range(0, coletas.Count).Where(i => i != origemIdx).ToList();
        var origem = coletas[origemIdx];

        var waypoints = outros.Count == 0
            ? null
            : "optimize:true|" + string.Join("|", outros.Select(i => $"{coletas[i].Latitude},{coletas[i].Longitude}"));

        var url = $"{baseUrl}/maps/api/directions/json"
            + $"?origin={origem.Latitude},{origem.Longitude}"
            + $"&destination={destino.Latitude},{destino.Longitude}"
            + (waypoints is null ? string.Empty : $"&waypoints={Uri.EscapeDataString(waypoints)}")
            + $"&mode=driving&key={ctx.ApiKey}";

        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<DirResposta>(cancellationToken: ct);
        var rota = payload?.Routes?.FirstOrDefault();
        if (payload is null || payload.Status != "OK" || rota is null) return null;

        var ordem = new List<int> { origemIdx };
        if (rota.WaypointOrder is { Count: > 0 })
        {
            foreach (var wi in rota.WaypointOrder)
            {
                if (wi >= 0 && wi < outros.Count) ordem.Add(outros[wi]);
            }
        }
        else
        {
            ordem.AddRange(outros);
        }

        var dist = rota.Legs?.Sum(l => l.Distance?.Value ?? 0) ?? 0;
        var dur = rota.Legs?.Sum(l => l.Duration?.Value ?? 0) ?? 0;
        return new RotaOtimizada(ordem, dist, dur);
    }

    private static double Haversine(Coordenada a, Coordenada b)
    {
        const double raio = 6_371_000;
        var dLat = (b.Latitude - a.Latitude) * Math.PI / 180;
        var dLon = (b.Longitude - a.Longitude) * Math.PI / 180;
        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(a.Latitude * Math.PI / 180) * Math.Cos(b.Latitude * Math.PI / 180)
               * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return raio * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    private sealed record DirResposta(string Status, List<Rota>? Routes);
    private sealed record Rota([property: JsonPropertyName("waypoint_order")] List<int>? WaypointOrder, List<Leg>? Legs);
    private sealed record Leg(Valor? Distance, Valor? Duration);
    private sealed record Valor(long Value, string? Text);
}

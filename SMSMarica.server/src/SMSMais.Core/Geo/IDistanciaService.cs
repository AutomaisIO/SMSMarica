using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Geo.Google;

namespace SMSMais.Core.Geo;

/// <summary>
/// Distância de carro entre dois pontos: usa o Google Distance Matrix quando configurado
/// (com cache), e cai para haversine (linha reta) quando indisponível. Usado pelo
/// faturamento (km a bordo) e pode ser adotado pelo motor de geração.
/// </summary>
public interface IDistanciaService
{
    Task<double> DistanciaMetrosAsync(Coordenada origem, Coordenada destino, CancellationToken ct = default);
}

public sealed class DistanciaService(
    IGoogleDistanceMatrixClient google,
    IMemoryCache cache,
    ILogger<DistanciaService> logger) : IDistanciaService
{
    public async Task<double> DistanciaMetrosAsync(Coordenada origem, Coordenada destino, CancellationToken ct = default)
    {
        var chave = $"tfd:dist:{origem.Latitude:F5},{origem.Longitude:F5}->{destino.Latitude:F5},{destino.Longitude:F5}";
        if (cache.TryGetValue(chave, out double cacheado)) return cacheado;

        double metros;
        try
        {
            metros = await google.DistanciaMetrosAsync(origem, destino, ct) ?? Haversine(origem, destino);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Distance Matrix indisponível; usando haversine.");
            metros = Haversine(origem, destino);
        }

        cache.Set(chave, metros, TimeSpan.FromHours(6));
        return metros;
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
}

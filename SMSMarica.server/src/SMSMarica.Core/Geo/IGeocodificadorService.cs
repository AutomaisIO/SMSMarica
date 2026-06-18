using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Geo;

/// <summary>
/// Geocodificação com cache local (schema smsmarica). Resolve endereços para
/// coordenadas via Google, sem tocar o hub FHIR. Endereços que não resolvem com
/// confiança entram na fila de revisão (pin manual). Ver ADR-0017.
/// </summary>
public interface IGeocodificadorService
{
    /// <summary>
    /// Resolve a coordenada de um endereço (cache → Google). Retorna null quando não
    /// foi possível resolver com confiança — o endereço entra na fila de revisão e a
    /// operação que chamou NÃO deve falhar por isso.
    /// </summary>
    Task<Coordenada?> GeocodificarAsync(Endereco? endereco, CancellationToken ct = default);

    Task<IReadOnlyList<GeocodigoDto>> ListarRevisaoPendenteAsync(CancellationToken ct = default);
    Task FixarManualAsync(FixarGeocodigoRequest request, CancellationToken ct = default);
}

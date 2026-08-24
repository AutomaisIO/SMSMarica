namespace SMSMais.Core.Geo;

/// <summary>Par de coordenadas geográficas (WGS84).</summary>
public sealed record Coordenada(double Latitude, double Longitude);

public sealed record GeocodigoDto(
    Guid Id,
    string EnderecoNormalizado,
    double Latitude,
    double Longitude,
    string Fonte,
    string? Precisao,
    bool RevisaoPendente,
    DateTime GeocodificadoEm);

/// <summary>Fixa manualmente a coordenada de um geocódigo pendente de revisão (pin no mapa).</summary>
public sealed record FixarGeocodigoRequest(Guid Id, double Latitude, double Longitude);

namespace SMSMarica.Data.Entities;

/// <summary>
/// Coordenada GPS reutilizável como propriedade owned em entidades.
/// </summary>
public sealed record Gps(double Latitude, double Longitude);

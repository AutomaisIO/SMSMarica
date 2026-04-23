using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

public class Geofence
{
    public Guid Id { get; set; }
    public TipoGeofence Tipo { get; set; }
    public Guid ReferenciaId { get; set; }
    public Gps Centro { get; set; } = new(0, 0);
    public int RaioMetros { get; set; }
    public DateTime CriadoEm { get; set; }
}

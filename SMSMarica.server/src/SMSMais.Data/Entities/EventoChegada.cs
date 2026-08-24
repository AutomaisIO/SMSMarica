namespace SMSMais.Data.Entities;

public class EventoChegada
{
    public Guid Id { get; set; }
    public Guid RotaDiariaId { get; set; }
    public Guid GeofenceId { get; set; }
    public DateTime OcorridoEm { get; set; }
    public DateTime CriadoEm { get; set; }

    public RotaDiaria? RotaDiaria { get; set; }
    public Geofence? Geofence { get; set; }
}

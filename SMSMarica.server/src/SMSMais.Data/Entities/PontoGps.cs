namespace SMSMais.Data.Entities;

public class PontoGps
{
    public Guid Id { get; set; }
    public Guid MotoristaId { get; set; }
    public Gps Coordenada { get; set; } = new(0, 0);
    public DateTime CapturadoEm { get; set; }
    public DateTime CriadoEm { get; set; }

    public Motorista? Motorista { get; set; }
}

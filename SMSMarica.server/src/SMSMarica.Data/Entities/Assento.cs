using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

public class Assento
{
    public Guid Id { get; set; }
    public Guid FileiraId { get; set; }
    public int Numero { get; set; }
    public TipoAssento Tipo { get; set; } = TipoAssento.Passageiro;
    public DateTime CriadoEm { get; set; }

    public Fileira? Fileira { get; set; }
}

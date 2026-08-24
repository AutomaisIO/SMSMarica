using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

public class Assento
{
    public Guid Id { get; set; }
    public Guid FileiraId { get; set; }
    public int Numero { get; set; }
    public TipoAssento Tipo { get; set; } = TipoAssento.Passageiro;
    public bool Bloqueado { get; set; } = false;
    public bool Excluido { get; set; } = false;
    public DateTime CriadoEm { get; set; }

    public Fileira? Fileira { get; set; }
}

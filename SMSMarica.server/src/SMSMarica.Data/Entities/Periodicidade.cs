using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

public class Periodicidade
{
    public Guid Id { get; set; }
    public Guid TratamentoId { get; set; }
    public TipoPeriodicidade Tipo { get; set; }
    public int? IntervaloDias { get; set; }
    public int? DiasSemanaMascara { get; set; }
    public DateOnly DataInicio { get; set; }
    public int QuantidadeSessoes { get; set; }
    public DateTime CriadoEm { get; set; }

    public Tratamento? Tratamento { get; set; }
}

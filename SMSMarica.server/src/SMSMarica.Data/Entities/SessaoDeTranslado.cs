using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

public class SessaoDeTranslado
{
    public Guid Id { get; set; }
    public Guid TratamentoId { get; set; }
    public DateOnly DataPrevista { get; set; }
    public StatusSessao Status { get; set; } = StatusSessao.Pendente;
    public DateTime CriadoEm { get; set; }

    public Tratamento? Tratamento { get; set; }
}

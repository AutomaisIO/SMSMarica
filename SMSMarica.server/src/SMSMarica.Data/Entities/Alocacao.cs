using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

public class Alocacao
{
    public Guid Id { get; set; }
    public Guid RotaDiariaId { get; set; }
    public Guid SessaoId { get; set; }
    public Guid AssentoId { get; set; }
    public TipoAlocacao Tipo { get; set; } = TipoAlocacao.Paciente;
    public DateTime CriadoEm { get; set; }

    public RotaDiaria? RotaDiaria { get; set; }
    public SessaoDeTranslado? Sessao { get; set; }
    public Assento? Assento { get; set; }
}

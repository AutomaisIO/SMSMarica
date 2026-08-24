using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

public class Alocacao
{
    public Guid Id { get; set; }
    public Guid RotaDiariaId { get; set; }
    public Guid SessaoId { get; set; }
    public Guid AssentoId { get; set; }
    public TipoAlocacao Tipo { get; set; } = TipoAlocacao.Paciente;

    // Sequenciamento da rota gerada (FT3/FT5)
    public int? OrdemParada { get; set; }
    public TipoParada Parada { get; set; } = TipoParada.Coleta;
    public DateTime? EtaPrevisto { get; set; }
    public TimeOnly? JanelaInicio { get; set; }
    public TimeOnly? JanelaFim { get; set; }

    public DateTime CriadoEm { get; set; }

    public RotaDiaria? RotaDiaria { get; set; }
    public SessaoDeTratamento? Sessao { get; set; }
    public Assento? Assento { get; set; }
}

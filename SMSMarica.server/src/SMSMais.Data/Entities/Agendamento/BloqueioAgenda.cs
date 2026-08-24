namespace SMSMais.Data.Entities.Agendamentos;

/// <summary>
/// Janela bloqueada de uma agenda (férias, feriado, ausência). Subtrai do pool de
/// horários: qualquer slot que se sobreponha ao bloqueio fica indisponível. Datas em
/// horário local (wall-clock). Ver ADR-0012.
/// </summary>
public class BloqueioAgenda
{
    public Guid Id { get; set; }

    public Guid AgendaId { get; set; }
    public Agenda? Agenda { get; set; }

    public DateTime InicioEm { get; set; }
    public DateTime FimEm { get; set; }

    public string Motivo { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}

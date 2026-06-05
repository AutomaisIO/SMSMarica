namespace SMSMarica.Data.Entities.Agendamentos;

/// <summary>
/// Janela de disponibilidade avulsa (mutirão, horário extra) fora da recorrência.
/// Soma-se ao pool de horários da agenda. Datas em horário local (wall-clock). Ver ADR-0012.
/// </summary>
public class DisponibilidadeAvulsa
{
    public Guid Id { get; set; }

    public Guid AgendaId { get; set; }
    public Agenda? Agenda { get; set; }

    public DateTime InicioEm { get; set; }
    public DateTime FimEm { get; set; }

    public string? Motivo { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}

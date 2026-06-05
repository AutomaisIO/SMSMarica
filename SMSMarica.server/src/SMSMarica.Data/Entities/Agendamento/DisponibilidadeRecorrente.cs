namespace SMSMarica.Data.Entities.Agendamentos;

/// <summary>
/// Regra de disponibilidade recorrente (semanal) de uma agenda — ex.: "toda segunda,
/// 08:00–12:00". É fonte do pool de horários; o cálculo de slots a fatia em consultas
/// de <see cref="Agenda.DuracaoConsultaMinutos"/>. Ver ADR-0012.
/// </summary>
public class DisponibilidadeRecorrente
{
    public Guid Id { get; set; }

    public Guid AgendaId { get; set; }
    public Agenda? Agenda { get; set; }

    /// <summary>Dia da semana (System.DayOfWeek: 0=Domingo … 6=Sábado).</summary>
    public DayOfWeek DiaSemana { get; set; }

    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFim { get; set; }

    /// <summary>Vigência opcional da regra (para grades temporárias). Null = enquanto a agenda valer.</summary>
    public DateOnly? VigenciaInicio { get; set; }
    public DateOnly? VigenciaFim { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}

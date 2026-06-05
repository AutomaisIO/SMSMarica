namespace SMSMarica.Data.Entities.Agendamentos;

/// <summary>
/// Grade de atendimento de um médico, numa unidade, para uma especialidade. É o
/// cabeçalho do agendamento: a partir dela e das disponibilidades (recorrentes/avulsas)
/// menos bloqueios e agendamentos, calculamos os horários livres sob demanda. Ver ADR-0012.
/// </summary>
public class Agenda
{
    public Guid Id { get; set; }

    public Guid UnidadeId { get; set; }
    public Unidade? Unidade { get; set; }

    public Guid EspecialidadeId { get; set; }
    public Especialidade? Especialidade { get; set; }

    /// <summary>Id do Practitioner no hub FHIR. Sem FK cross-system (ADR-0010/0012).</summary>
    public Guid MedicoId { get; set; }

    /// <summary>Snapshot do nome do médico (FHIR) para exibir a grade sem ir ao hub a cada render.</summary>
    public string MedicoNome { get; set; } = string.Empty;

    /// <summary>Snapshot opcional do CNS do médico.</summary>
    public string? MedicoCns { get; set; }

    /// <summary>Duração padrão de cada consulta/slot, em minutos.</summary>
    public int DuracaoConsultaMinutos { get; set; } = 20;

    public DateOnly VigenciaInicio { get; set; }
    public DateOnly? VigenciaFim { get; set; }

    public bool Ativo { get; set; } = true;

    public ICollection<DisponibilidadeRecorrente> Recorrencias { get; set; } = [];
    public ICollection<DisponibilidadeAvulsa> Avulsos { get; set; } = [];
    public ICollection<BloqueioAgenda> Bloqueios { get; set; } = [];
    public ICollection<Agendamento> Agendamentos { get; set; } = [];

    // Auditoria ADR-0006
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}

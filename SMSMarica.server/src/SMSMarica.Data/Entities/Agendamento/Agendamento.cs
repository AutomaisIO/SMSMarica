using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities.Agendamentos;

/// <summary>
/// Consulta marcada para um paciente em um horário de uma agenda. Ocupa o pool de
/// horários (slot indisponível enquanto não cancelado). O paciente vem do FHIR (Patient):
/// guardamos só id (sem FK cross-system) + snapshot de nome/CNS. Ver ADR-0012.
/// </summary>
public class Agendamento
{
    public Guid Id { get; set; }

    public Guid AgendaId { get; set; }
    public Agenda? Agenda { get; set; }

    /// <summary>Id do Patient no hub FHIR. Sem FK cross-system (ADR-0010/0012).</summary>
    public Guid PacienteId { get; set; }

    /// <summary>Snapshot do nome do paciente (FHIR) para exibir a grade.</summary>
    public string PacienteNome { get; set; } = string.Empty;

    /// <summary>Snapshot opcional do CNS do paciente.</summary>
    public string? PacienteCns { get; set; }

    public DateTime InicioEm { get; set; }
    public DateTime FimEm { get; set; }

    public StatusAgendamento Status { get; set; } = StatusAgendamento.Agendado;

    public string? Observacao { get; set; }

    public DateTime? ConfirmadoEm { get; set; }
    public DateTime? RealizadoEm { get; set; }
    public DateTime? CanceladoEm { get; set; }
    public string? MotivoCancelamento { get; set; }

    // Auditoria ADR-0006
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}

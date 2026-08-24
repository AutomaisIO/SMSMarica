using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Agendamentos;

/// <summary>
/// Grade de atendimento de um recurso, numa unidade. O recurso varia por
/// <see cref="Finalidade"/> (ver ADR-0013):
/// <list type="bullet">
/// <item>Consulta + especialidade, sem médico → agenda da especialidade (pool, qualquer médico);</item>
/// <item>Consulta + especialidade + médico → agenda de médico específico (retorno);</item>
/// <item>Exame + equipamento → agenda de equipamento (imagem).</item>
/// </list>
/// A partir dela e das disponibilidades (recorrentes/avulsas) menos bloqueios e
/// agendamentos, calculamos os horários livres sob demanda — igual para os três tipos.
/// </summary>
public class Agenda
{
    public Guid Id { get; set; }

    public FinalidadeAgenda Finalidade { get; set; } = FinalidadeAgenda.Consulta;

    public Guid UnidadeId { get; set; }
    public Unidade? Unidade { get; set; }

    /// <summary>Especialidade (consulta). Null em agendas de exame.</summary>
    public Guid? EspecialidadeId { get; set; }
    public Especialidade? Especialidade { get; set; }

    /// <summary>
    /// Id do Practitioner no hub FHIR (consulta de médico específico). Sem FK cross-system.
    /// <b>Null = agenda da especialidade</b> (pool, qualquer médico). Ver ADR-0013.
    /// </summary>
    public Guid? MedicoId { get; set; }

    /// <summary>Snapshot do nome do médico (FHIR), quando a agenda é de médico específico.</summary>
    public string? MedicoNome { get; set; }
    public string? MedicoCns { get; set; }

    /// <summary>Equipamento (exame). Null em agendas de consulta.</summary>
    public Guid? EquipamentoId { get; set; }
    public Equipamento? Equipamento { get; set; }

    /// <summary>Duração padrão de cada horário/slot, em minutos (consulta ou exame).</summary>
    public int DuracaoSlotMinutos { get; set; } = 20;

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

using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Agendamentos.Dtos;

/// <summary>Horário livre calculado de uma agenda (wall-clock local).</summary>
public sealed record SlotLivreDto(DateTime InicioEm, DateTime FimEm);

/// <summary>
/// Horário livre agregado por especialidade: além da janela, identifica a agenda, a
/// unidade e o MÉDICO dono da agenda — o fluxo de marcação é especialidade → escolhe o
/// médico → escolhe o horário (a agenda é sempre do profissional). Médico null só em
/// agendas legadas de pool, exibidas como "Equipe da especialidade".
/// </summary>
public sealed record SlotEspecialidadeDto(
    Guid AgendaId,
    Guid UnidadeId,
    string UnidadeNome,
    Guid? MedicoId,
    string? MedicoNome,
    DateTime InicioEm,
    DateTime FimEm);

public sealed record AgendamentoDto(
    Guid Id,
    Guid AgendaId,
    Guid PacienteId,
    string PacienteNome,
    string? PacienteCns,
    DateTime InicioEm,
    DateTime FimEm,
    StatusAgendamento Status,
    Guid? TipoExameId,
    string? Observacao,
    DateTime CriadoEm);

public sealed record AgendamentoListItemDto(
    Guid Id,
    Guid PacienteId,
    string PacienteNome,
    DateTime InicioEm,
    DateTime FimEm,
    StatusAgendamento Status);

/// <summary>
/// Marca uma consulta. <see cref="InicioEm"/> deve ser exatamente o início de um slot
/// livre da agenda; o fim é derivado da duração da agenda.
/// </summary>
public sealed record AgendarRequest(
    Guid AgendaId,
    Guid PacienteId,
    DateTime InicioEm,
    Guid? TipoExameId,
    string? Observacao);

public sealed record CancelarAgendamentoRequest(string? Motivo);

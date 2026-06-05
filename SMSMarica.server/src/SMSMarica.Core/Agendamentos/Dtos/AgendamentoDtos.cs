using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Agendamentos.Dtos;

/// <summary>Horário livre calculado de uma agenda (wall-clock local).</summary>
public sealed record SlotLivreDto(DateTime InicioEm, DateTime FimEm);

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

namespace SMSMarica.Core.Agendamentos.Dtos;

public sealed record AgendaDto(
    Guid Id,
    Guid UnidadeId,
    string UnidadeNome,
    Guid EspecialidadeId,
    string EspecialidadeNome,
    Guid MedicoId,
    string MedicoNome,
    string? MedicoCns,
    int DuracaoConsultaMinutos,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    bool Ativo,
    IReadOnlyList<DisponibilidadeRecorrenteDto> Recorrencias,
    DateTime CriadoEm);

public sealed record AgendaListItemDto(
    Guid Id,
    string UnidadeNome,
    string EspecialidadeNome,
    Guid MedicoId,
    string MedicoNome,
    int DuracaoConsultaMinutos,
    bool Ativo);

public sealed record CadastrarAgendaRequest(
    Guid UnidadeId,
    Guid EspecialidadeId,
    Guid MedicoId,
    int DuracaoConsultaMinutos,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim);

public sealed record AtualizarAgendaRequest(
    int DuracaoConsultaMinutos,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    bool Ativo);

public sealed record DisponibilidadeRecorrenteDto(
    Guid Id,
    DayOfWeek DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    DateOnly? VigenciaInicio,
    DateOnly? VigenciaFim,
    bool Ativo);

public sealed record AdicionarRecorrenciaRequest(
    DayOfWeek DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    DateOnly? VigenciaInicio,
    DateOnly? VigenciaFim);

public sealed record DisponibilidadeAvulsaDto(
    Guid Id,
    DateTime InicioEm,
    DateTime FimEm,
    string? Motivo);

public sealed record AdicionarAvulsoRequest(
    DateTime InicioEm,
    DateTime FimEm,
    string? Motivo);

public sealed record BloqueioAgendaDto(
    Guid Id,
    DateTime InicioEm,
    DateTime FimEm,
    string Motivo);

public sealed record AdicionarBloqueioRequest(
    DateTime InicioEm,
    DateTime FimEm,
    string Motivo);

/// <summary>Disponibilidades pontuais (avulsos + bloqueios) de uma agenda num intervalo, para a tela de gestão.</summary>
public sealed record DisponibilidadesAgendaDto(
    IReadOnlyList<DisponibilidadeAvulsaDto> Avulsos,
    IReadOnlyList<BloqueioAgendaDto> Bloqueios);

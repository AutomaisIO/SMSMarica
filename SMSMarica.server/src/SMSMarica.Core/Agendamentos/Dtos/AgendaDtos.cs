using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Agendamentos.Dtos;

public sealed record AgendaDto(
    Guid Id,
    FinalidadeAgenda Finalidade,
    Guid UnidadeId,
    string UnidadeNome,
    Guid? EspecialidadeId,
    string? EspecialidadeNome,
    Guid? MedicoId,
    string? MedicoNome,
    string? MedicoCns,
    Guid? EquipamentoId,
    string? EquipamentoNome,
    string Alvo,
    int DuracaoSlotMinutos,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    bool Ativo,
    IReadOnlyList<DisponibilidadeRecorrenteDto> Recorrencias,
    DateTime CriadoEm);

public sealed record AgendaListItemDto(
    Guid Id,
    FinalidadeAgenda Finalidade,
    string UnidadeNome,
    string Alvo,
    int DuracaoSlotMinutos,
    bool Ativo);

/// <summary>
/// Cadastro de agenda. Por finalidade: Consulta exige <see cref="EspecialidadeId"/> E
/// <see cref="MedicoId"/> — a agenda é sempre DO PROFISSIONAL (a marcação parte da
/// especialidade e lista os médicos dela; não existe mais "pool" solto da especialidade).
/// Exame exige <see cref="EquipamentoId"/>. <see cref="Recorrencias"/> permite criar a
/// agenda já com a grade semanal completa (padrão de mercado), em vez de criar vazia
/// e adicionar horários depois.
/// </summary>
public sealed record CadastrarAgendaRequest(
    FinalidadeAgenda Finalidade,
    Guid UnidadeId,
    Guid? EspecialidadeId,
    Guid? MedicoId,
    Guid? EquipamentoId,
    int DuracaoSlotMinutos,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    IReadOnlyList<AdicionarRecorrenciaRequest>? Recorrencias = null);

public sealed record AtualizarAgendaRequest(
    int DuracaoSlotMinutos,
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

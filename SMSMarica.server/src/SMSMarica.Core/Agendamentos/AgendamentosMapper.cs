using SMSMarica.Core.Agendamentos.Dtos;
using SMSMarica.Data.Entities.Agendamentos;

namespace SMSMarica.Core.Agendamentos;

internal static class AgendamentosMapper
{
    public static AgendaDto ParaDto(Agenda a) => new(
        a.Id,
        a.UnidadeId,
        a.Unidade?.Nome ?? string.Empty,
        a.EspecialidadeId,
        a.Especialidade?.Nome ?? string.Empty,
        a.MedicoId,
        a.MedicoNome,
        a.MedicoCns,
        a.DuracaoConsultaMinutos,
        a.VigenciaInicio,
        a.VigenciaFim,
        a.Ativo,
        [.. a.Recorrencias.OrderBy(r => r.DiaSemana).ThenBy(r => r.HoraInicio).Select(ParaDto)],
        a.CriadoEm);

    public static AgendaListItemDto ParaListItem(Agenda a) => new(
        a.Id,
        a.Unidade?.Nome ?? string.Empty,
        a.Especialidade?.Nome ?? string.Empty,
        a.MedicoId,
        a.MedicoNome,
        a.DuracaoConsultaMinutos,
        a.Ativo);

    public static DisponibilidadeRecorrenteDto ParaDto(DisponibilidadeRecorrente r) => new(
        r.Id,
        r.DiaSemana,
        r.HoraInicio,
        r.HoraFim,
        r.VigenciaInicio,
        r.VigenciaFim,
        r.Ativo);

    public static DisponibilidadeAvulsaDto ParaDto(DisponibilidadeAvulsa d) => new(
        d.Id,
        d.InicioEm,
        d.FimEm,
        d.Motivo);

    public static BloqueioAgendaDto ParaDto(BloqueioAgenda b) => new(
        b.Id,
        b.InicioEm,
        b.FimEm,
        b.Motivo);

    public static AgendamentoDto ParaDto(Agendamento a) => new(
        a.Id,
        a.AgendaId,
        a.PacienteId,
        a.PacienteNome,
        a.PacienteCns,
        a.InicioEm,
        a.FimEm,
        a.Status,
        a.Observacao,
        a.CriadoEm);

    public static AgendamentoListItemDto ParaListItem(Agendamento a) => new(
        a.Id,
        a.PacienteId,
        a.PacienteNome,
        a.InicioEm,
        a.FimEm,
        a.Status);
}

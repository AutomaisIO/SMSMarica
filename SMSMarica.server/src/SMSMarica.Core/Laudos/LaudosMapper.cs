using SMSMarica.Core.Laudos.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Laudos;

internal static class LaudosMapper
{
    public static LaudoDto ParaDto(Laudo l) => new(
        l.Id,
        l.StudyInstanceUID,
        l.Versao,
        l.LaudoAnteriorId,
        l.PacienteId,
        l.Paciente?.Usuario?.NomeCompleto,
        l.Paciente?.Usuario?.Cpf,
        l.MedicoId,
        l.MedicoNomeSnapshot ?? l.Medico?.Usuario?.NomeCompleto ?? string.Empty,
        l.MedicoCrmSnapshot ?? l.Medico?.Crm ?? string.Empty,
        l.MedicoUfCrmSnapshot ?? l.Medico?.UfCrm ?? string.Empty,
        l.MedicoRqeSnapshot ?? l.Medico?.Rqe,
        l.LaudoTemplateId,
        l.LaudoTemplate?.Nome,
        l.Titulo,
        l.ConteudoJson,
        l.ConteudoHtml,
        l.Status,
        l.FinalizadoEm,
        l.CriadoEm,
        l.AtualizadoEm);

    public static LaudoListItemDto ParaListItem(Laudo l) => new(
        l.Id,
        l.StudyInstanceUID,
        l.Versao,
        l.PacienteId,
        l.Paciente?.Usuario?.NomeCompleto,
        l.MedicoId,
        l.MedicoNomeSnapshot ?? l.Medico?.Usuario?.NomeCompleto ?? string.Empty,
        l.Titulo,
        l.Status,
        l.FinalizadoEm,
        l.CriadoEm);

    public static LaudoHistoricoItemDto ParaHistoricoItem(Laudo l) => new(
        l.Id,
        l.Versao,
        l.Status,
        l.MedicoId,
        l.MedicoNomeSnapshot ?? l.Medico?.Usuario?.NomeCompleto ?? string.Empty,
        l.CriadoEm,
        l.FinalizadoEm);
}

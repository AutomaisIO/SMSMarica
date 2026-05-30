using SMSMarica.Core.Laudos.Dtos;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Fhir;
using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Core.Laudos;

internal static class LaudosMapper
{
    public static LaudoDto ParaDto(Laudo l) => new(
        l.Id,
        l.StudyInstanceUID,
        l.Versao,
        l.LaudoAnteriorId,
        l.PatientId,
        NomePaciente(l.Patient),
        CpfPaciente(l.Patient),
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
        l.PatientId,
        NomePaciente(l.Patient),
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

    private static string? NomePaciente(Patient? p) =>
        p?.Names.FirstOrDefault(n => n.Use == NameUse.Official)?.Text
        ?? p?.Names.FirstOrDefault()?.Text;

    private static string? CpfPaciente(Patient? p) =>
        p?.Identifiers.FirstOrDefault(i => i.Type == IdentifierTypeCode.Cpf)?.Value;
}

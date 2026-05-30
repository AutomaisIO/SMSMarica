using SMSMarica.Core.Laudos.Dtos;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Fhir;
using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Core.Laudos;

internal static class LaudosMapper
{
    private const string SystemRqe = "urn:br:rqe";
    private const string CouncilCrm = "CRM";

    public static LaudoDto ParaDto(Laudo l) => new(
        l.Id,
        l.StudyInstanceUID,
        l.Versao,
        l.LaudoAnteriorId,
        l.PatientId,
        NomePaciente(l.Patient),
        CpfPaciente(l.Patient),
        l.PractitionerId,
        l.PractitionerNomeSnapshot ?? NomePractitioner(l.Practitioner) ?? string.Empty,
        l.PractitionerCrmSnapshot ?? CrmPractitioner(l.Practitioner) ?? string.Empty,
        l.PractitionerUfCrmSnapshot ?? UfCrmPractitioner(l.Practitioner) ?? string.Empty,
        l.PractitionerRqeSnapshot ?? RqePractitioner(l.Practitioner),
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
        l.PractitionerId,
        l.PractitionerNomeSnapshot ?? NomePractitioner(l.Practitioner) ?? string.Empty,
        l.Titulo,
        l.Status,
        l.FinalizadoEm,
        l.CriadoEm);

    public static LaudoHistoricoItemDto ParaHistoricoItem(Laudo l) => new(
        l.Id,
        l.Versao,
        l.Status,
        l.PractitionerId,
        l.PractitionerNomeSnapshot ?? NomePractitioner(l.Practitioner) ?? string.Empty,
        l.CriadoEm,
        l.FinalizadoEm);

    private static string? NomePaciente(Patient? p) =>
        p?.Names.FirstOrDefault(n => n.Use == NameUse.Official)?.Text
        ?? p?.Names.FirstOrDefault()?.Text;

    private static string? CpfPaciente(Patient? p) =>
        p?.Identifiers.FirstOrDefault(i => i.Type == IdentifierTypeCode.Cpf)?.Value;

    private static string? NomePractitioner(Practitioner? p) =>
        p?.Names.FirstOrDefault(n => n.Use == NameUse.Official)?.Text
        ?? p?.Names.FirstOrDefault()?.Text;

    private static string? CrmPractitioner(Practitioner? p) =>
        p?.Qualifications.FirstOrDefault(q => q.CouncilCode == CouncilCrm)?.CouncilNumber;

    private static string? UfCrmPractitioner(Practitioner? p) =>
        p?.Qualifications.FirstOrDefault(q => q.CouncilCode == CouncilCrm)?.CouncilState;

    private static string? RqePractitioner(Practitioner? p) =>
        p?.Identifiers.FirstOrDefault(i => i.System == SystemRqe)?.Value;
}

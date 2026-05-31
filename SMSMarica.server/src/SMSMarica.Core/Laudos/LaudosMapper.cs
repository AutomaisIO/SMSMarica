using SMSMarica.Core.Laudos.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Laudos;

internal static class LaudosMapper
{
    // Nome/CPF do paciente NÃO vêm mais embutidos (paciente vive no hub FHIR).
    // O consumidor resolve via GET /pacientes/{PacienteId} (que proxia o FHIR).
    // Médico usa os snapshots gravados na finalização. TODO: resolver nome do
    // paciente via FHIR quando precisar embutir nas listagens.
    public static LaudoDto ParaDto(Laudo l) => new(
        l.Id,
        l.StudyInstanceUID,
        l.Versao,
        l.LaudoAnteriorId,
        l.PacienteId,
        null,
        null,
        l.MedicoId,
        l.MedicoNomeSnapshot ?? string.Empty,
        l.MedicoCrmSnapshot ?? string.Empty,
        l.MedicoUfCrmSnapshot ?? string.Empty,
        l.MedicoRqeSnapshot,
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
        null,
        l.MedicoId,
        l.MedicoNomeSnapshot ?? string.Empty,
        l.Titulo,
        l.Status,
        l.FinalizadoEm,
        l.CriadoEm);

    public static LaudoHistoricoItemDto ParaHistoricoItem(Laudo l) => new(
        l.Id,
        l.Versao,
        l.Status,
        l.MedicoId,
        l.MedicoNomeSnapshot ?? string.Empty,
        l.CriadoEm,
        l.FinalizadoEm);
}

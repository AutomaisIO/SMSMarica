using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Fhir;
using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Core.SolicitacoesExame;

internal static class SolicitacoesExameMapper
{
    public static SolicitacaoExameDto ParaDto(SolicitacaoExame s) => new(
        s.Id,
        s.AccessionNumber,
        s.StudyInstanceUID,
        s.WorklistItemUid,
        s.PatientId,
        NomePaciente(s.Patient) ?? string.Empty,
        CpfPaciente(s.Patient),
        CnsPaciente(s.Patient),
        s.TipoExameId,
        s.TipoExame?.Nome ?? string.Empty,
        s.TipoExame?.ModalidadeDicom ?? ModalidadeDicom.OT,
        s.UnidadeId,
        s.Unidade?.Nome ?? string.Empty,
        s.SolicitanteUsuarioId,
        s.SolicitanteNome,
        s.SolicitanteCrm,
        s.SolicitanteUfCrm,
        s.NumeroRegulacaoSus,
        s.Justificativa,
        s.Status,
        s.Prioridade,
        s.Observacoes,
        s.DataAgendada,
        s.IniciadoEm,
        s.RealizadoEm,
        s.ErroIntegracaoPacs,
        s.CanceladoEm,
        s.MotivoCancelamento,
        s.TentativasEnvio,
        s.UltimaTentativaEm,
        s.ProximaTentativaEm,
        s.CriadoEm,
        s.AtualizadoEm);

    public static SolicitacaoExameListItemDto ParaListItem(SolicitacaoExame s) => new(
        s.Id,
        s.AccessionNumber,
        s.PatientId,
        NomePaciente(s.Patient) ?? string.Empty,
        s.TipoExameId,
        s.TipoExame?.Nome ?? string.Empty,
        s.TipoExame?.ModalidadeDicom ?? ModalidadeDicom.OT,
        s.SolicitanteNome,
        s.Status,
        s.Prioridade,
        s.DataAgendada,
        s.CriadoEm);

    private static string? NomePaciente(Patient? p) =>
        p?.Names.FirstOrDefault(n => n.Use == NameUse.Official)?.Text
        ?? p?.Names.FirstOrDefault()?.Text;

    private static string? CpfPaciente(Patient? p) =>
        p?.Identifiers.FirstOrDefault(i => i.Type == IdentifierTypeCode.Cpf)?.Value;

    private static string? CnsPaciente(Patient? p) =>
        p?.Identifiers.FirstOrDefault(i => i.Type == IdentifierTypeCode.Cns)?.Value;
}

using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame;

internal static class SolicitacoesExameMapper
{
    public static SolicitacaoExameDto ParaDto(SolicitacaoExame s) => new(
        s.Id,
        s.AccessionNumber,
        s.StudyInstanceUID,
        s.WorklistItemUid,
        s.PacienteId,
        // Paciente vive no hub FHIR — consumidor resolve via GET /pacientes/{PacienteId}. TODO embutir.
        string.Empty,
        null,
        null,
        s.TipoExameId,
        s.TipoExame?.Nome ?? string.Empty,
        s.TipoExame?.ModalidadeDicom ?? ModalidadeDicom.OT,
        s.UnidadeId,
        s.Unidade?.Nome ?? string.Empty,
        s.UnidadeSolicitanteId,
        s.UnidadeSolicitante?.Nome,
        s.SolicitanteUsuarioId,
        s.SolicitanteNome,
        s.SolicitanteNumConselho,
        s.SolicitanteUfConselho,
        s.SolicitanteConselho,
        s.CodigoSolicitacao,
        s.ChaveConfirmacao,
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
        s.AtualizadoEm,
        s.DataEstudo);

    public static SolicitacaoExameListItemDto ParaListItem(SolicitacaoExame s) => new(
        s.Id,
        s.AccessionNumber,
        s.PacienteId,
        string.Empty,
        s.TipoExameId,
        s.TipoExame?.Nome ?? string.Empty,
        s.TipoExame?.ModalidadeDicom ?? ModalidadeDicom.OT,
        s.SolicitanteNome,
        s.Status,
        s.Prioridade,
        s.DataAgendada,
        s.CriadoEm,
        s.DataEstudo,
        s.StudyInstanceUID,
        null,
        false);
}

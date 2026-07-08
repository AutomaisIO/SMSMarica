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
        s.SolicitanteNome,
        s.CodigoSolicitacao,
        s.ChaveConfirmacao,
        s.Justificativa,
        s.Status,
        s.Prioridade,
        s.Observacoes,
        s.DataSolicitacao,
        s.DataRegulacao,
        s.DataAgendada,
        s.IniciadoEm,
        s.RealizadoEm,
        s.ErroIntegracaoPacs,
        s.CanceladoEm,
        s.MotivoCancelamento,
        s.StatusConfirmacao,
        s.ConfirmadoEm,
        s.ConfirmadoCanal,
        s.ConfirmacaoCanceladaEm,
        s.MotivoCancelamentoPaciente,
        s.AutorizadoEm,
        s.AutorizadoPor,
        false, // PacienteContatoVerificado — calculado no enriquecimento (consulta contato_validado).
        s.TentativasEnvio,
        s.UltimaTentativaEm,
        s.ProximaTentativaEm,
        s.CriadoEm,
        s.AtualizadoEm,
        s.DataEstudo,
        s.RawSisreg);

    public static SolicitacaoExameListItemDto ParaListItem(SolicitacaoExame s, Guid? unidadeReferencia = null)
    {
        // Direção relativa à unidade ativa: executora → Recebida; solicitante → Enviada.
        // Quando a mesma unidade é executora e solicitante, prevalece Recebida (é a que atua).
        DirecaoSolicitacao? direcao = unidadeReferencia is { } r
            ? s.UnidadeId == r ? DirecaoSolicitacao.Recebida
            : s.UnidadeSolicitanteId == r ? DirecaoSolicitacao.Enviada
            : null
            : null;

        return new(
            s.Id,
            s.AccessionNumber,
            s.CodigoSolicitacao,
            s.PacienteId,
            string.Empty,
            s.TipoExameId,
            s.TipoExame?.Nome ?? string.Empty,
            s.TipoExame?.ModalidadeDicom ?? ModalidadeDicom.OT,
            s.Unidade?.Nome ?? string.Empty,
            s.SolicitanteNome,
            s.Status,
            s.StatusConfirmacao,
            s.AutorizadoEm,
            s.ErroIntegracaoPacs,
            s.Prioridade,
            s.DataAgendada,
            s.CriadoEm,
            s.DataEstudo,
            s.StudyInstanceUID,
            null,
            false,
            direcao);
    }
}

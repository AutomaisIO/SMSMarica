using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame;

/// <summary>
/// Mapeia um exame de imagem (<see cref="ExameImagem"/> + sua <see cref="Solicitacao"/> de regulação)
/// para os DTOs. O <c>Id</c> exposto é o do satélite (id público preservado — ADR-0021); o status é
/// o de EXECUÇÃO (worklist/PACS); os campos de regulação vêm de <c>s.Solicitacao</c>.
/// </summary>
internal static class SolicitacoesExameMapper
{
    public static SolicitacaoExameDto ParaDto(ExameImagem s)
    {
        var reg = s.Solicitacao!;
        return new(
            s.Id,
            s.AccessionNumber,
            s.StudyInstanceUID,
            s.WorklistItemUid,
            reg.PacienteId,
            // Paciente vive no hub FHIR — consumidor resolve via GET /pacientes/{PacienteId}. TODO embutir.
            string.Empty,
            null,
            null,
            s.TipoExameId ?? Guid.Empty,
            s.TipoExame?.Nome ?? string.Empty,
            s.TipoExame?.ModalidadeDicom ?? ModalidadeDicom.OT,
            reg.UnidadeExecutanteId,
            reg.UnidadeExecutante?.Nome ?? string.Empty,
            reg.UnidadeSolicitanteId,
            reg.UnidadeSolicitante?.Nome,
            reg.SolicitanteNome,
            reg.CodigoSolicitacao,
            reg.ChaveConfirmacao,
            reg.Justificativa,
            s.Status,
            reg.Prioridade,
            reg.Observacoes,
            reg.DataSolicitacao,
            reg.DataRegulacao,
            reg.DataAgendada,
            s.IniciadoEm,
            s.RealizadoEm,
            s.ErroIntegracaoPacs,
            reg.CanceladoEm,
            reg.MotivoCancelamento,
            reg.StatusConfirmacao,
            reg.ConfirmadoEm,
            reg.ConfirmadoCanal,
            reg.ConfirmacaoCanceladaEm,
            reg.MotivoCancelamentoPaciente,
            reg.AutorizadoEm,
            reg.AutorizadoPor,
            false, // PacienteContatoVerificado — calculado no enriquecimento (marcador FHIR).
            s.TentativasEnvio,
            s.UltimaTentativaEm,
            s.ProximaTentativaEm,
            s.CriadoEm,
            s.AtualizadoEm,
            s.DataEstudo,
            reg.RawSisreg);
    }

    public static SolicitacaoExameListItemDto ParaListItem(ExameImagem s, Guid? unidadeReferencia = null)
    {
        var reg = s.Solicitacao!;
        // Direção relativa à unidade ativa: executora → Recebida; solicitante → Enviada.
        // Quando a mesma unidade é executora e solicitante, prevalece Recebida (é a que atua).
        DirecaoSolicitacao? direcao = unidadeReferencia is { } r
            ? reg.UnidadeExecutanteId == r ? DirecaoSolicitacao.Recebida
            : reg.UnidadeSolicitanteId == r ? DirecaoSolicitacao.Enviada
            : null
            : null;

        return new(
            s.Id,
            s.AccessionNumber,
            reg.CodigoSolicitacao,
            reg.PacienteId,
            string.Empty,
            s.TipoExameId ?? Guid.Empty,
            s.TipoExame?.Nome ?? string.Empty,
            s.TipoExame?.ModalidadeDicom ?? ModalidadeDicom.OT,
            reg.UnidadeExecutante?.Nome ?? string.Empty,
            reg.SolicitanteNome,
            s.Status,
            reg.StatusConfirmacao,
            reg.AutorizadoEm,
            s.ErroIntegracaoPacs,
            reg.Prioridade,
            reg.DataAgendada,
            s.CriadoEm,
            s.DataEstudo,
            s.StudyInstanceUID,
            null,
            false,
            direcao);
    }
}

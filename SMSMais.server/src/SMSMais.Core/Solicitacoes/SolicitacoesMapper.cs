using SMSMais.Core.Solicitacoes.Dtos;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Solicitacoes;

/// <summary>
/// Mapeia uma <see cref="Solicitacao"/> (espinha de regulação) e, quando houver, o seu exame de
/// imagem (<see cref="ExameImagem"/>) para os DTOs. O <c>Id</c> exposto é o PÚBLICO — o do exame
/// quando há satélite (preservado do modelo antigo, ADR-0021), senão o da espinha. Com exame, o
/// status é o de EXECUÇÃO (worklist/PACS); sem exame, o da regulação na mesma régua
/// (<see cref="StatusComum"/>).
/// </summary>
internal static class SolicitacoesMapper
{
    /// <summary>
    /// Status da regulação na régua da lista. Os quatro estados da espinha existem com o mesmo
    /// nome no status de execução; é o que deixa exame e consulta dividirem selo e filtro.
    /// </summary>
    public static StatusSolicitacaoExame StatusComum(StatusSolicitacao status) => status switch
    {
        StatusSolicitacao.Agendada => StatusSolicitacaoExame.Agendada,
        StatusSolicitacao.Realizada => StatusSolicitacaoExame.Realizada,
        StatusSolicitacao.Cancelada => StatusSolicitacaoExame.Cancelada,
        _ => StatusSolicitacaoExame.Solicitada,
    };

    /// <summary>
    /// Inverso de <see cref="StatusComum"/> para o filtro: o estado da espinha que corresponde ao
    /// status pedido. Null = estado só de execução (Enviada, Em execução, Laudada…), que nenhuma
    /// solicitação sem exame de imagem tem.
    /// </summary>
    public static StatusSolicitacao? StatusDaEspinha(StatusSolicitacaoExame status) => status switch
    {
        StatusSolicitacaoExame.Solicitada => StatusSolicitacao.Solicitada,
        StatusSolicitacaoExame.Agendada => StatusSolicitacao.Agendada,
        StatusSolicitacaoExame.Realizada => StatusSolicitacao.Realizada,
        StatusSolicitacaoExame.Cancelada => StatusSolicitacao.Cancelada,
        _ => null,
    };

    public static SolicitacaoDto ParaDto(ExameImagem exame) => ParaDto(exame.Solicitacao!, exame);

    public static SolicitacaoDto ParaDto(Solicitacao reg, ExameImagem? exame)
    {
        return new(
            exame?.Id ?? reg.Id,
            reg.Id,
            reg.Categoria,
            exame?.AccessionNumber ?? string.Empty,
            exame?.StudyInstanceUID ?? string.Empty,
            exame?.WorklistItemUid,
            reg.PacienteId,
            // Paciente vive no hub FHIR — consumidor resolve via GET /pacientes/{PacienteId}. TODO embutir.
            string.Empty,
            null,
            null,
            exame?.TipoExameId ?? Guid.Empty,
            exame?.TipoExame?.Nome ?? string.Empty,
            exame?.TipoExame?.ModalidadeDicom ?? ModalidadeDicom.OT,
            reg.ProcedimentoTexto,
            reg.EspecialidadeTexto,
            reg.ProcedimentoSigtapCodigo,
            reg.UnidadeExecutanteId,
            reg.UnidadeExecutante?.Nome ?? string.Empty,
            reg.UnidadeSolicitanteId,
            reg.UnidadeSolicitante?.Nome,
            reg.SolicitanteNome,
            reg.CodigoSolicitacao,
            reg.ChaveConfirmacao,
            reg.Justificativa,
            exame?.Status ?? StatusComum(reg.Status),
            reg.Prioridade,
            reg.Observacoes,
            reg.DataSolicitacao,
            reg.DataRegulacao,
            reg.DataAgendada,
            exame?.IniciadoEm,
            exame?.RealizadoEm,
            exame?.ErroIntegracaoPacs,
            reg.CanceladoEm,
            reg.MotivoCancelamento,
            reg.StatusConfirmacao,
            reg.ConfirmadoEm,
            reg.ConfirmadoCanal,
            reg.ConfirmacaoCanceladaEm,
            reg.MotivoCancelamentoPaciente,
            reg.AutorizadoEm,
            reg.AutorizadoPor,
            null, // AutorizadoPorNome — resolvido no enriquecimento do detalhe (ticket #30).
            false, // PacienteContatoVerificado — calculado no enriquecimento (marcador FHIR).
            exame?.TentativasEnvio ?? 0,
            exame?.UltimaTentativaEm,
            exame?.ProximaTentativaEm,
            exame?.CriadoEm ?? reg.CriadoEm,
            exame?.AtualizadoEm ?? reg.AtualizadoEm,
            exame?.DataEstudo,
            reg.RawSisreg,
            exame?.EquipamentoId,
            exame?.Equipamento?.Nome,
            exame?.Equipamento?.IdentificadorDicom,
            TipoVaga: reg.TipoVaga,
            // CID materializado; na ausência dele (importações anteriores ao ticket #155), lê do RAW.
            // A descrição por extenso é resolvida no enriquecimento do detalhe (precisa do catálogo).
            CidCodigo: reg.CidCodigo
                ?? Integracoes.SisregWeb.Importacao.AgendaTxtParser.CidDe(reg.RawSisreg));
    }

    public static SolicitacaoListItemDto ParaListItem(Solicitacao reg, Guid? unidadeReferencia = null)
    {
        var exame = reg.ExameImagem;
        return new(
            exame?.Id ?? reg.Id,
            reg.Id,
            reg.Categoria,
            exame?.AccessionNumber ?? string.Empty,
            reg.CodigoSolicitacao,
            reg.PacienteId,
            string.Empty,
            null,
            exame?.TipoExameId ?? Guid.Empty,
            exame?.TipoExame?.Nome ?? string.Empty,
            exame?.TipoExame?.ModalidadeDicom ?? ModalidadeDicom.OT,
            reg.ProcedimentoTexto,
            reg.EspecialidadeTexto,
            reg.UnidadeExecutante?.Nome ?? string.Empty,
            reg.SolicitanteNome,
            exame?.Status ?? StatusComum(reg.Status),
            reg.StatusConfirmacao,
            reg.AutorizadoEm,
            exame?.ErroIntegracaoPacs,
            reg.Prioridade,
            reg.DataAgendada,
            exame?.CriadoEm ?? reg.CriadoEm,
            exame?.DataEstudo,
            exame?.StudyInstanceUID ?? string.Empty,
            null,
            false,
            // Quando a mesma unidade é executora e solicitante, prevalece Recebida (é a que atua).
            Common.Unidades.SolicitacaoNoEscopo.Direcao(unidadeReferencia, reg.UnidadeExecutanteId, reg.UnidadeSolicitanteId),
            TipoVaga: reg.TipoVaga);
    }
}

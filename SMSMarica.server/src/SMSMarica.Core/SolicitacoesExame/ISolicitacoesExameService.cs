using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame;

public interface ISolicitacoesExameService
{
    /// <summary>
    /// Envio MANUAL do resultado ao paciente (botão "Enviar exame" / "Enviar laudo" no detalhe).
    /// Valida a prontidão (exame Realizado/Laudado para <c>ExameLiberado</c>; laudo assinado para
    /// <c>LaudoPronto</c>) e dispara a comunicação na hora, registrando origem=Manual + quem enviou.
    /// <paramref name="assumirRisco"/> permite enviar mesmo sem telefone verificado.
    /// </summary>
    Task EnviarComunicacaoManualAsync(
        Guid solicitacaoExameId, FinalidadeComunicacao finalidade, bool assumirRisco,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitacaoExameListItemDto>> ListarAsync(
        FiltroSolicitacoesDto filtro,
        CancellationToken cancellationToken = default);

    Task<SolicitacaoExameDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SolicitacaoExameDto?> ObterPorAccessionAsync(string accession, CancellationToken cancellationToken = default);

    Task<SolicitacaoExameDto?> ObterPorStudyAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(CadastrarSolicitacaoExameRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarSolicitacaoExameRequest request, CancellationToken cancellationToken = default);

    Task CancelarAsync(Guid id, CancelarSolicitacaoExameRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Autorização presencial na recepção: grava a chave, marca AutorizadoEm e — só então —
    /// enfileira o envio ao PACS (se o tipo envia à worklist). Exige que o paciente tenha um
    /// número VERIFICADO (senão lança ValidacaoException). Se a confirmação ainda estava
    /// pendente, marca Confirmada com canal "presencial".
    /// </summary>
    Task AutorizarAsync(Guid id, string chaveConfirmacao, CancellationToken cancellationToken = default);

    /// <summary>Refaz o POST UPS-RS quando a primeira tentativa falhou (status ainda Solicitada).</summary>
    Task ReenviarWorklistAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exclui (soft-delete) a solicitação. Antes, remove o item de worklist do
    /// dcm4chee e confirma (anti-lixo); se a remoção no PACS falhar e
    /// <paramref name="force"/> for false, lança ConflitoException
    /// ("solicitacaoExame.exclusao_pacs_falhou"). Com force=true, ignora o PACS e
    /// limpa apenas a base local.
    /// </summary>
    Task ExcluirAsync(Guid id, bool force, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca a solicitação como Laudada — chamado pelo módulo de Laudos ao
    /// finalizar um laudo cujo StudyInstanceUID bate com uma solicitação aberta.
    /// </summary>
    Task MarcarComoLaudadaAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca como Realizada (chamado pelo SincronizadorExamesService/ExameAssociacaoService
    /// quando detecta o study no PACS). <paramref name="realizadoEm"/> é a hora de detecção
    /// pelo servidor (auditoria); <paramref name="dataEstudo"/> é a data/hora REAL de execução
    /// vinda do DICOM (StudyDate/StudyTime) — fonte da verdade da data do exame. Null quando o
    /// PACS não trouxe a tag.
    /// </summary>
    Task MarcarComoRealizadaAsync(Guid id, DateTime realizadoEm, DateTime? dataEstudo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa uma tentativa do worker no fluxo de envio resiliente:
    /// - Solicitada → POST UPS-RS; se OK vira Enviada.
    /// - Enviada → GET no workitem; se OK vira Agendada; se 404 volta a Solicitada.
    /// - Em falha (rede/erro do PACS): incrementa contador, recalcula
    ///   <c>ProximaTentativaEm</c> com backoff exponencial.
    /// Idempotente — pode ser chamado várias vezes.
    /// </summary>
    Task ProcessarTentativaEnvioAsync(Guid solicitacaoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove do dcm4chee o item de worklist de um exame que não deveria mais estar lá —
    /// exame já realizado/laudado/cancelado, ou excluído. Faz parte do fluxo: o exame chega,
    /// o motor tira o item da lista. Sem MPPS, é isso que mantém a worklist do equipamento
    /// fiel ao que ainda falta fazer.
    /// <para>
    /// Ao confirmar a remoção zera <c>WorklistItemUid</c> — o campo é o espelho do que está
    /// no PACS. Em falha, agenda nova tentativa (o item volta na próxima passagem do worker).
    /// Exame apenas AGENDADO e não realizado (paciente faltou) nunca é removido por aqui:
    /// sai só por cancelamento/exclusão explícitos.
    /// </para>
    /// Idempotente — pode ser chamado várias vezes.
    /// </summary>
    Task ProcessarLimpezaWorklistAsync(Guid exameId, CancellationToken cancellationToken = default);
}

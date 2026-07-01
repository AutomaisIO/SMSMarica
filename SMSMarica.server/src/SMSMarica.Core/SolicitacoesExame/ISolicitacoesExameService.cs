using SMSMarica.Core.SolicitacoesExame.Dtos;

namespace SMSMarica.Core.SolicitacoesExame;

public interface ISolicitacoesExameService
{
    Task<IReadOnlyList<SolicitacaoExameListItemDto>> ListarAsync(
        FiltroSolicitacoesDto filtro,
        CancellationToken cancellationToken = default);

    Task<SolicitacaoExameDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SolicitacaoExameDto?> ObterPorAccessionAsync(string accession, CancellationToken cancellationToken = default);

    Task<SolicitacaoExameDto?> ObterPorStudyAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(CadastrarSolicitacaoExameRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarSolicitacaoExameRequest request, CancellationToken cancellationToken = default);

    Task CancelarAsync(Guid id, CancelarSolicitacaoExameRequest request, CancellationToken cancellationToken = default);

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
}

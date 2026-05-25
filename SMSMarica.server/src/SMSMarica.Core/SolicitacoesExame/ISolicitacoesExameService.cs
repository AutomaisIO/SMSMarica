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

    Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca a solicitação como Laudada — chamado pelo módulo de Laudos ao
    /// finalizar um laudo cujo StudyInstanceUID bate com uma solicitação aberta.
    /// </summary>
    Task MarcarComoLaudadaAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca como Realizada (chamado pelo SincronizadorExamesService quando
    /// detecta o study no PACS).
    /// </summary>
    Task MarcarComoRealizadaAsync(Guid id, DateTime realizadoEm, CancellationToken cancellationToken = default);
}

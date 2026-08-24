using SMSMais.Core.Avaliacoes.Dtos;

namespace SMSMais.Core.Avaliacoes;

public interface IAvaliacoesService
{
    Task<IReadOnlyList<AvaliacaoListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<AvaliacaoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> RegistrarAsync(RegistrarAvaliacaoRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarAvaliacaoRequest request, CancellationToken cancellationToken = default);
    Task DeletarAsync(Guid id, CancellationToken cancellationToken = default);
}

using SMSMais.Core.TiposTratamento.Dtos;

namespace SMSMais.Core.TiposTratamento;

public interface ITiposTratamentoService
{
    Task<IReadOnlyList<TipoTratamentoListItemDto>> ListarAsync(bool somenteAtivos, CancellationToken cancellationToken = default);
    Task<TipoTratamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarTipoTratamentoRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarTipoTratamentoRequest request, CancellationToken cancellationToken = default);
    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);
}

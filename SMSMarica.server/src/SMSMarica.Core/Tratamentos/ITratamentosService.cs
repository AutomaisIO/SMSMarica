using SMSMarica.Core.Tratamentos.Dtos;

namespace SMSMarica.Core.Tratamentos;

public interface ITratamentosService
{
    Task<IReadOnlyList<TratamentoListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<TratamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarTratamentoRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarTratamentoRequest request, CancellationToken cancellationToken = default);
    Task EncerrarAsync(Guid id, CancellationToken cancellationToken = default);
}

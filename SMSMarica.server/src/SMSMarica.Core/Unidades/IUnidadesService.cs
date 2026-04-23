using SMSMarica.Core.Unidades.Dtos;

namespace SMSMarica.Core.Unidades;

public interface IUnidadesService
{
    Task<IReadOnlyList<UnidadeListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<UnidadeDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarUnidadeRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarUnidadeRequest request, CancellationToken cancellationToken = default);
    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);
}

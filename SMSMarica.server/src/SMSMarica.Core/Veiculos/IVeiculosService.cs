using SMSMarica.Core.Veiculos.Dtos;

namespace SMSMarica.Core.Veiculos;

public interface IVeiculosService
{
    Task<IReadOnlyList<VeiculoListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<VeiculoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarVeiculoRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarVeiculoRequest request, CancellationToken cancellationToken = default);
    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);

    // Gestão de fileiras e assentos do veículo (aggregate children).
    Task<FileiraDto> AdicionarFileiraAsync(Guid veiculoId, AdicionarFileiraRequest request, CancellationToken cancellationToken = default);
    Task RemoverFileiraAsync(Guid veiculoId, Guid fileiraId, CancellationToken cancellationToken = default);
}

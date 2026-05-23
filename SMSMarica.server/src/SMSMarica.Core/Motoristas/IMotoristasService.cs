using SMSMarica.Core.Motoristas.Dtos;

namespace SMSMarica.Core.Motoristas;

public interface IMotoristasService
{
    Task<IReadOnlyList<MotoristaListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<MotoristaDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarMotoristaRequest request, CancellationToken cancellationToken = default);
    Task<Guid> PromoverAsync(PromoverMotoristaRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarMotoristaRequest request, CancellationToken cancellationToken = default);
    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);
}

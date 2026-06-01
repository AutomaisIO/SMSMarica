using SMSMarica.Core.Medicos.Dtos;

namespace SMSMarica.Core.Medicos;

public interface IMedicosService
{
    Task<IReadOnlyList<MedicoListItemDto>> BuscarAsync(string? termo, CancellationToken cancellationToken = default);
    Task<MedicoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarMedicoRequest request, CancellationToken cancellationToken = default);
    Task<Guid> PromoverAsync(PromoverMedicoRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarMedicoRequest request, CancellationToken cancellationToken = default);
    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);
}

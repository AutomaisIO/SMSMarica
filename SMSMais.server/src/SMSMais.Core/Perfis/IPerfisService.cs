using SMSMais.Core.Perfis.Dtos;

namespace SMSMais.Core.Perfis;

public interface IPerfisService
{
    Task<IReadOnlyList<PerfilListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<PerfilDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarPerfilRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarPerfilRequest request, CancellationToken cancellationToken = default);
    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);
}

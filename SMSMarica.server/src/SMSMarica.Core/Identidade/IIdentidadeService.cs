using SMSMarica.Core.Identidade.Dtos;

namespace SMSMarica.Core.Identidade;

public interface IIdentidadeService
{
    Task<IReadOnlyList<UsuarioListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<UsuarioDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarUsuarioRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarUsuarioRequest request, CancellationToken cancellationToken = default);
    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);
}

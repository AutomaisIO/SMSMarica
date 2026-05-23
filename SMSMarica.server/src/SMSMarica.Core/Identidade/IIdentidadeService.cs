using SMSMarica.Core.Identidade.Dtos;

namespace SMSMarica.Core.Identidade;

public interface IIdentidadeService
{
    Task<LoginRespostaDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<PermissoesResolvidasDto> ObterPermissoesResolvidasAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsuarioListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<UsuarioDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarUsuarioRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarUsuarioRequest request, CancellationToken cancellationToken = default);
    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);

    Task AtualizarPerfisDoUsuarioAsync(Guid usuarioId, AtualizarPerfisDoUsuarioRequest request, CancellationToken cancellationToken = default);
    Task AtualizarOverridesDoUsuarioAsync(Guid usuarioId, AtualizarOverridesDoUsuarioRequest request, CancellationToken cancellationToken = default);
    Task AlterarSenhaAsync(Guid usuarioId, AlterarSenhaRequest request, CancellationToken cancellationToken = default);
}

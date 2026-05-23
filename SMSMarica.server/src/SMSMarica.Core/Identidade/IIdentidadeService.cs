using SMSMarica.Core.Identidade.Dtos;

namespace SMSMarica.Core.Identidade;

public interface IIdentidadeService
{
    Task<LoginRespostaDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<PermissoesResolvidasDto> ObterPermissoesResolvidasAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsuarioListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<UsuarioDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Retorna o usuário com este CPF (qualquer estado), ou <c>null</c> se não existir.</summary>
    Task<UsuarioDto?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarUsuarioRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarUsuarioRequest request, CancellationToken cancellationToken = default);
    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Usuário atualiza apenas foto/telefone/endereço da própria conta.</summary>
    Task AtualizarMinhaContaAsync(Guid usuarioId, AtualizarMinhaContaRequest request, CancellationToken cancellationToken = default);

    Task AtualizarPerfisDoUsuarioAsync(Guid usuarioId, AtualizarPerfisDoUsuarioRequest request, CancellationToken cancellationToken = default);
    Task AtualizarOverridesDoUsuarioAsync(Guid usuarioId, AtualizarOverridesDoUsuarioRequest request, CancellationToken cancellationToken = default);

    /// <summary>Admin define a senha de outro usuário e opcionalmente força troca no próximo login.</summary>
    Task AlterarSenhaAsync(Guid usuarioId, AlterarSenhaRequest request, CancellationToken cancellationToken = default);

    /// <summary>Admin gera uma senha aleatória forte; força troca no próximo login.</summary>
    Task<SenhaGeradaDto> GerarNovaSenhaAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Usuário troca a própria senha (exige a antiga) e desativa a flag de troca obrigatória.</summary>
    Task AlterarMinhaSenhaAsync(Guid usuarioId, AlterarMinhaSenhaRequest request, CancellationToken cancellationToken = default);
}

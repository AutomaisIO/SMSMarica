using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;

namespace SMSMais.Tests.Infraestrutura;

/// <summary>
/// Credencial de integração dublada. Por padrão finge "ainda não configurada" — que é o caminho que
/// os motores tratam devolvendo agendamento desligado em vez de erro.
/// </summary>
internal sealed class CredencialFake : IIntegracaoCredencialService
{
    public Task<IReadOnlyList<IntegracaoCredencialDto>> ListarAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IntegracaoCredencialDto>>([]);

    public Task<IntegracaoCredencialDto> ObterAsync(string provedor, CancellationToken cancellationToken = default) =>
        throw new ValidacaoException("credencial.nao_configurada", "Credencial não configurada (fake).");

    public Task AtualizarAsync(
        string provedor, AtualizarIntegracaoCredencialRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task LimparAsync(string provedor, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IntegracaoCredencialContexto> ObterContextoAsync(string provedor, CancellationToken cancellationToken = default) =>
        throw new ValidacaoException("credencial.nao_configurada", "Credencial não configurada (fake).");
}

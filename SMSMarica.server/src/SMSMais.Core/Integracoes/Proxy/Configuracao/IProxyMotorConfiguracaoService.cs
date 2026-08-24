namespace SMSMais.Core.Integracoes.Proxy.Configuracao;

/// <summary>
/// Configuração dos motores de proxy (CPF/CEP): listagem/edição para a tela (RBAC
/// IntegracoesConfig, token write-only) e resolução dos motores ativos para os
/// orquestradores (token revelado, com fallback do appsettings).
/// </summary>
public interface IProxyMotorConfiguracaoService
{
    /// <summary>Lista os motores suportados do serviço, com placeholders para os não configurados.</summary>
    Task<IReadOnlyList<ProxyMotorDto>> ListarAsync(string servico, CancellationToken cancellationToken = default);

    /// <summary>Upsert de um motor: cifra o token só quando preenchido; vazio mantém o atual.</summary>
    Task AtualizarAsync(
        string servico, string motor, AtualizarProxyMotorRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Motores ativos do serviço, em ordem de fallback, com token revelado e parâmetros de
    /// execução. Uso interno dos orquestradores — nunca exposto ao front.
    /// </summary>
    Task<IReadOnlyList<(string Motor, MotorExecucao Cfg)>> ObterMotoresAtivosAsync(
        string servico, CancellationToken cancellationToken = default);

    /// <summary>
    /// Config de execução de um motor específico (token revelado + fallback), <b>ignorando</b>
    /// o flag ativo — usado pelo teste manual, que precisa probar um motor antes de ativá-lo.
    /// </summary>
    Task<MotorExecucao> ObterExecucaoAsync(string servico, string motor, CancellationToken cancellationToken = default);
}

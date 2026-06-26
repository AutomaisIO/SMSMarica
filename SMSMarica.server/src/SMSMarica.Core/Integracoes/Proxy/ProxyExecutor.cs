using System.Text.Json;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;

namespace SMSMarica.Core.Integracoes.Proxy;

/// <summary>
/// Executa uma consulta sobre uma cadeia de motores com fallback. Para cada motor: tenta
/// até <c>Tentativas</c> vezes, cada tentativa com timeout próprio (CTS), tratando timeout/
/// rede/JSON inválido como falha transitória. Negativa autoritativa
/// (<see cref="MotorNaoEncontrouException"/>) não retenta o motor, mas passa ao próximo.
/// Só devolve erro ao usuário quando todos os motores se esgotam.
/// </summary>
internal static class ProxyExecutor
{
    public static async Task<T> ExecutarAsync<T>(
        string servico,
        IReadOnlyList<(string Motor, MotorExecucao Cfg)> motores,
        Func<string, MotorExecucao, CancellationToken, Task<T>> chamar,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (motores.Count == 0)
        {
            throw new ConflitoException(
                $"proxy.{servico}.sem_motor",
                $"Nenhum motor de {ServicosProxy.Rotulo(servico)} configurado e ativo.");
        }

        var rotulo = ServicosProxy.Rotulo(servico);
        string? mensagemNegativa = null;

        foreach (var (motor, cfg) in motores)
        {
            var tentativasMax = Math.Clamp(cfg.Tentativas, 1, 5);
            for (var tentativa = 1; tentativa <= tentativasMax; tentativa++)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(cfg.TimeoutSegundos, 1, 60)));
                try
                {
                    return await chamar(motor, cfg, cts.Token);
                }
                catch (MotorNaoEncontrouException ex)
                {
                    // Resposta determinística: não adianta retentar este motor.
                    mensagemNegativa = ex.Message;
                    logger.LogInformation("Motor {Motor} ({Servico}) negou a consulta: {Msg}", motor, rotulo, ex.Message);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // cancelamento real do cliente — propaga
                }
                catch (Exception ex) when (
                    ex is OperationCanceledException or HttpRequestException or JsonException or MotorIndisponivelException)
                {
                    logger.LogWarning(
                        ex, "Motor {Motor} ({Servico}) falhou (tentativa {N}/{Max}).", motor, rotulo, tentativa, tentativasMax);
                    // tentativa esgotada → próxima iteração (retry) ou próximo motor
                }
            }
        }

        if (mensagemNegativa is not null)
        {
            throw new ValidacaoException($"proxy.{servico}.nao_encontrado", mensagemNegativa);
        }

        throw new ConflitoException(
            $"proxy.{servico}.indisponivel",
            $"O serviço de {rotulo} não respondeu agora. Tente novamente em instantes.");
    }
}

using System.Diagnostics;
using System.Text.Json;
using SMSMais.Core.Integracoes.Dtos;

namespace SMSMais.Core.Integracoes.Proxy;

/// <summary>Resultado do teste manual de um motor de CPF (sempre HTTP 200; sucesso/erro no corpo).</summary>
public sealed record ProxyTesteCpfResultado(bool Ok, string? Mensagem, HubCpfRespostaDto? Resultado, long DuracaoMs);

/// <summary>Resultado do teste manual de um motor de CEP.</summary>
public sealed record ProxyTesteCepResultado(bool Ok, string? Mensagem, HubCepRespostaDto? Resultado, long DuracaoMs);

/// <summary>
/// Executa <b>uma única tentativa</b> de um motor específico (sem fallback), com o timeout
/// configurado, traduzindo qualquer falha numa mensagem amigável. Usado pelos testes manuais
/// da tela de Integrações.
/// </summary>
internal static class ProxyTeste
{
    public static async Task<(bool Ok, string? Mensagem, T? Resultado, long DuracaoMs)> ExecutarAsync<T>(
        MotorExecucao cfg,
        Func<CancellationToken, Task<T>> chamar,
        CancellationToken cancellationToken) where T : class
    {
        var cronometro = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(cfg.TimeoutSegundos, 1, 60)));
        try
        {
            var r = await chamar(cts.Token);
            return (true, "Motor respondeu com sucesso.", r, cronometro.ElapsedMilliseconds);
        }
        catch (MotorNaoEncontrouException ex)
        {
            return (false, ex.Message, null, cronometro.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // cancelamento real do cliente
        }
        catch (OperationCanceledException)
        {
            return (false, $"Timeout: o motor não respondeu em {cfg.TimeoutSegundos}s.", null, cronometro.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or MotorIndisponivelException)
        {
            return (false, $"Falha: {ex.Message}", null, cronometro.ElapsedMilliseconds);
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;

/// <summary>
/// Consome a fila de resolução de pendências e roda FORA da request. Sem isto, um lote de 260
/// pendências (cada uma com uma ida ao CADSUS) morreria no timeout do HTTP, ou quando o operador
/// fechasse a aba.
/// </summary>
public sealed class ResolucaoPendenciasRunner(
    IResolucaoPendenciasFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<ResolucaoPendenciasRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in fila.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Escopo próprio: o da request já morreu quando o lote começa.
                using var scope = scopeFactory.CreateScope();
                var servico = scope.ServiceProvider.GetRequiredService<IResolucaoPendenciasService>();
                await servico.ExecutarAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Sem este catch, BackgroundServiceExceptionBehavior=StopHost derruba a API inteira
                // por causa de uma pendência ruim.
                logger.LogError(ex, "Erro inesperado na resolução em lote {ExecucaoId}.", job.ExecucaoId);
            }
        }
    }
}

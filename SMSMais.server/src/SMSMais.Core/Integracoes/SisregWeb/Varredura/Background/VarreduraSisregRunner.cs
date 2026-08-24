using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Integracoes.SisregWeb.Varredura.Background;

/// <summary>
/// Consome a fila e roda a varredura FORA da request: uma varredura leva minutos, e fechar a aba
/// não pode matá-la no meio.
/// </summary>
public sealed class VarreduraSisregRunner(
    IVarreduraSisregFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<VarreduraSisregRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in fila.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Escopo próprio: o da request já morreu quando a varredura começa.
                using var scope = scopeFactory.CreateScope();
                var servico = scope.ServiceProvider.GetRequiredService<IVarreduraAgendaService>();
                await servico.ExecutarAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Sem este catch, BackgroundServiceExceptionBehavior=StopHost derruba a API inteira
                // por causa da varredura de uma unidade.
                logger.LogError(ex, "Erro inesperado na varredura {ExecucaoId} da unidade {UnidadeId}.",
                    job.ExecucaoId, job.UnidadeId);
            }
        }
    }
}

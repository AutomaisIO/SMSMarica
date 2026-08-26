using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Background;

/// <summary>
/// Consome a fila e roda o lote FORA da request: sincronizar a rede inteira leva minutos, e fechar
/// a aba não pode matá-lo no meio.
/// </summary>
public sealed class MapeamentoLoteRunner(
    IMapeamentoLoteFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<MapeamentoLoteRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in fila.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Escopo próprio: o da request já morreu quando o lote começa.
                using var scope = scopeFactory.CreateScope();
                var servico = scope.ServiceProvider.GetRequiredService<ISisregMapeamentoLoteService>();
                await servico.ExecutarAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Sem este catch, BackgroundServiceExceptionBehavior=StopHost derrubaria a API inteira.
                logger.LogError(ex, "Erro inesperado no lote de sincronização do mapeamento SISREG.");
            }
        }
    }
}

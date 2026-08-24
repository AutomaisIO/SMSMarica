using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;

/// <summary>
/// Consome a fila de lotes e roda a importação FORA da request (fechar a aba não mata mais nada).
/// </summary>
public sealed class SisregImportacaoRunner(
    ISisregImportacaoFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<SisregImportacaoRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in fila.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Escopo próprio: o da request já morreu quando o lote começa a rodar.
                using var scope = scopeFactory.CreateScope();
                var servico = scope.ServiceProvider.GetRequiredService<IImportacaoLoteService>();
                await servico.ExecutarAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Sem este catch, BackgroundServiceExceptionBehavior=StopHost derruba a API inteira
                // por causa de um arquivo ruim.
                logger.LogError(ex, "Erro inesperado processando o lote {LoteId} de importação SISREG.", job.LoteId);
            }
        }
    }
}

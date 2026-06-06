using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMarica.Core.Integracoes.Pep.Background;

/// <summary>
/// Consome a fila de importações de PEP e executa uma de cada vez, cada qual no seu escopo
/// de DI. O trabalho pesado (Oracle + hub) roda aqui fora do request HTTP — o disparo só
/// enfileira e responde 202.
/// </summary>
public sealed class PepSincronizacaoRunner(
    IPepSincronizacaoFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<PepSincronizacaoRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PepSincronizacaoRunner iniciado — aguardando importações.");

        await foreach (var job in fila.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var servico = scope.ServiceProvider.GetRequiredService<IPepSincronizacaoService>();
                await servico.ExecutarAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado processando importação {Id}.", job.ExecucaoId);
            }
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Alertas;

/// <summary>
/// Drena a fila de avisos, um por vez, cada um num escopo próprio. Um por vez de propósito: o
/// freio lê e grava a mesma linha da fonte, e dois avisos da mesma fonte em paralelo passariam os
/// dois pelo freio.
/// </summary>
public sealed class AlertaPlataformaWorker(
    AlertaPlataformaFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<AlertaPlataformaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var evento in fila.Leitor.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var despachante = scope.ServiceProvider.GetRequiredService<AlertaPlataformaDespachante>();
                    await despachante.ProcessarAsync(evento, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Warning, não Error: o próprio aviso não pode entrar na captura do log e
                    // realimentar a fila (a categoria também está excluída, por garantia).
                    logger.LogWarning(ex, "ALERTA: não foi possível processar o aviso {Chave}.", evento.Chave);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

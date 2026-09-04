using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Integracoes.SisregWeb.Escalas.Background;

/// <summary>
/// Consome a fila e roda FORA da request: baixar 5,9 MB e fazer upsert de 17 mil linhas leva mais
/// que um timeout de HTTP, e fechar a aba não pode matar isso no meio.
/// </summary>
public sealed class EscalasSincronizacaoRunner(
    IEscalasSincronizacaoFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<EscalasSincronizacaoRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in fila.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Escopo próprio: o da request já morreu quando a sincronização começa.
                using var scope = scopeFactory.CreateScope();
                var servico = scope.ServiceProvider.GetRequiredService<IEscalasSincronizacaoService>();
                await servico.ExecutarAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Sem este catch, BackgroundServiceExceptionBehavior=StopHost derrubaria a API.
                logger.LogError(ex, "Erro inesperado no sincronismo de escalas do SISREG.");
            }
        }
    }
}

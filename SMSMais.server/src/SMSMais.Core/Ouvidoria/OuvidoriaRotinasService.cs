using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Ouvidoria;

/// <summary>
/// Rotinas automáticas da ouvidoria (plano §2.4 e §2.7): arquiva o que ficou sem complementação
/// além do prazo e conclui o que foi respondido e não teve recurso. Roda a cada 6 h (primeira
/// vez 2 min após subir) num escopo próprio. Sem usuário no contexto, o service age como sistema.
/// </summary>
public sealed class OuvidoriaRotinasService(
    IServiceScopeFactory scopeFactory,
    ILogger<OuvidoriaRotinasService> logger) : BackgroundService
{
    private static readonly TimeSpan AtrasoInicial = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(AtrasoInicial, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var servico = scope.ServiceProvider.GetRequiredService<IOuvidoriaManifestacaoService>();

                var arquivadas = await servico.ArquivarSemComplementacaoAsync(stoppingToken);
                var concluidas = await servico.ConcluirRespondidasSemRecursoAsync(stoppingToken);
                if (arquivadas > 0 || concluidas > 0)
                {
                    logger.LogInformation("Ouvidoria: rotina arquivou {Arquivadas} sem complementação e concluiu {Concluidas} sem recurso.",
                        arquivadas, concluidas);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Falha de um ciclo não derruba o motor; o próximo tenta de novo.
                logger.LogError(ex, "Ouvidoria: falha no ciclo das rotinas automáticas.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}

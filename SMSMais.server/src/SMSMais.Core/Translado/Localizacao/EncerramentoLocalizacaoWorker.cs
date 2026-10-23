using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Translado.Localizacao;

/// <summary>
/// A cada minuto, encerra (e apaga a posição de) quem compartilha a localização mas saiu da espera por um
/// caminho que não passa pelo tablet: viagem cancelada no painel, outro dia, já em casa (ADR-0079 §4).
/// Embarque e ausência encerram na hora, na própria marcação do tablet.
/// </summary>
public sealed class EncerramentoLocalizacaoWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<EncerramentoLocalizacaoWorker> logger) : BackgroundService
{
    private static readonly TimeSpan AtrasoInicial = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(AtrasoInicial, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var servico = scope.ServiceProvider.GetRequiredService<ICompartilhamentoLocalizacaoService>();
                var encerrados = await servico.EncerrarForaDaEsperaAsync(stoppingToken);
                if (encerrados > 0)
                    logger.LogInformation("Localização: {Encerrados} compartilhamento(s) encerrado(s) fora da espera.", encerrados);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Falha de um ciclo não derruba a rotina; o próximo tenta de novo.
                logger.LogError(ex, "Localização: falha ao encerrar os compartilhamentos fora da espera.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}

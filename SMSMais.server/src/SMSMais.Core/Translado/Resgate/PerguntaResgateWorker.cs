using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Translado.Resgate;

/// <summary>
/// A cada minuto, pergunta "você já está disponível para retorno?" a quem passou da previsão do
/// procedimento e ainda não disse que está no local — e de novo 10 minutos depois de cada "não, ainda não
/// terminei", até ele dizer que sim (decisão de 19/10). A resposta alimenta a lista dos motoristas.
/// </summary>
public sealed class PerguntaResgateWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PerguntaResgateWorker> logger) : BackgroundService
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
                var resgates = scope.ServiceProvider.GetRequiredService<IResgateService>();
                var perguntados = await resgates.PerguntarQuemPassouDaPrevisaoAsync(stoppingToken);
                if (perguntados > 0)
                    logger.LogInformation("Resgate: {Perguntados} paciente(s) perguntado(s) se já estão disponíveis.", perguntados);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Falha de um ciclo não derruba a rotina; o próximo tenta de novo.
                logger.LogError(ex, "Resgate: falha ao perguntar a disponibilidade dos pacientes.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}

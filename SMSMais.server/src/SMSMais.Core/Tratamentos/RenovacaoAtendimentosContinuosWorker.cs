using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Tempo;

namespace SMSMais.Core.Tratamentos;

/// <summary>
/// Renova os atendimentos contínuos de mês em mês: mantém as sessões geradas até o fim do mês
/// seguinte. Contínuo nunca é "infinito" — encerrar o atendimento ou o óbito no cadastro param a
/// renovação. Roda a cada 6 h (primeira vez 2 min após subir), idempotente, num escopo próprio.
/// </summary>
public sealed class RenovacaoAtendimentosContinuosWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RenovacaoAtendimentosContinuosWorker> logger) : BackgroundService
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
                var servico = scope.ServiceProvider.GetRequiredService<ITratamentosService>();

                var renovados = await servico.RenovarContinuosAsync(FusoBrasilia.HojeEmBrasilia(), stoppingToken);
                if (renovados > 0)
                {
                    logger.LogInformation("Transporte: {Renovados} atendimento(s) contínuo(s) renovado(s) até o fim do mês seguinte.",
                        renovados);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Falha de um ciclo não derruba o motor; o próximo tenta de novo.
                logger.LogError(ex, "Transporte: falha na renovação dos atendimentos contínuos.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}

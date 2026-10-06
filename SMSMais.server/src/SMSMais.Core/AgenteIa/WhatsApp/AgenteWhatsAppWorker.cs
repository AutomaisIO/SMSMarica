using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.AgenteIa.WhatsApp;

/// <summary>
/// Motor do canal WhatsApp do Agente IA (ADR-0068). Com turno rodando, passa a cada
/// <see cref="IntervaloAtivo"/> (o andamento precisa sair no ritmo de ~30 s); parado, dorme até o
/// sinal do webhook ou <see cref="IntervaloOcioso"/> — o pedido gravado depois do sinal (o
/// manipulador sinaliza antes do commit) é pego aí.
/// </summary>
public sealed class AgenteWhatsAppWorker(
    IServiceScopeFactory scopeFactory,
    SinalAgenteWhatsApp sinal,
    ILogger<AgenteWhatsAppWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IntervaloAtivo = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan IntervaloOcioso = TimeSpan.FromSeconds(10);

    /// <summary>Folga entre o sinal e a leitura: o webhook ainda está gravando o pedido.</summary>
    private static readonly TimeSpan FolgaDoSinal = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var rodando = false;
            try
            {
                using var scope = scopeFactory.CreateScope();
                rodando = await scope.ServiceProvider.GetRequiredService<IAgenteWhatsAppProcessador>()
                    .ProcessarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Agente IA pelo WhatsApp: passagem falhou.");
            }

            try
            {
                if (rodando)
                {
                    await Task.Delay(IntervaloAtivo, stoppingToken);
                    continue;
                }

                using var espera = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                espera.CancelAfter(IntervaloOcioso);
                try
                {
                    await sinal.Leitor.ReadAsync(espera.Token);
                    await Task.Delay(FolgaDoSinal, stoppingToken);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { /* intervalo */ }
            }
            catch (OperationCanceledException) { return; }
        }
    }
}

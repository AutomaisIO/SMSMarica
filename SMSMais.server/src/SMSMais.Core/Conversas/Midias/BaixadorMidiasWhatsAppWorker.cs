using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Conversas.Midias;

/// <summary>
/// Sinal do webhook para o worker: "chegou mídia". O webhook não baixa nada — o relay espera a
/// resposta por poucos segundos, e um PDF grande vindo da Meta não cabe nisso.
/// </summary>
public sealed class SinalMidiasWhatsApp
{
    private readonly Channel<bool> _canal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Sinalizar() => _canal.Writer.TryWrite(true);

    internal ChannelReader<bool> Leitor => _canal.Reader;
}

/// <summary>
/// Baixa pelo Zap as mídias que o paciente mandou no WhatsApp e as guarda como pendentes. Acorda
/// pelo sinal do webhook e, de qualquer forma, a cada meio minuto — o que ficou para trás num
/// reinício ou numa falha passageira é retomado sozinho.
/// </summary>
public sealed class BaixadorMidiasWhatsAppWorker(
    IServiceScopeFactory scopeFactory,
    SinalMidiasWhatsApp sinal,
    ILogger<BaixadorMidiasWhatsAppWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);
    private const int PorPassagem = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processadas;
                do
                {
                    using var scope = scopeFactory.CreateScope();
                    var servico = scope.ServiceProvider.GetRequiredService<IMidiasConversaService>();
                    processadas = await servico.ProcessarRecebidasAsync(PorPassagem, stoppingToken);
                }
                while (processadas == PorPassagem && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Baixador de mídias do WhatsApp: passagem falhou.");
            }

            using var espera = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            espera.CancelAfter(Intervalo);
            try { await sinal.Leitor.ReadAsync(espera.Token); }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { /* intervalo */ }
            catch (OperationCanceledException) { return; }
        }
    }
}

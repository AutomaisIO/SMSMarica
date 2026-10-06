using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Notificacoes.WhatsApp.Transcricao;

/// <summary>
/// Sinal do webhook para o worker: "chegou áudio para transcrever". O webhook não transcreve — o
/// relay espera a resposta por poucos segundos e o STT leva 1–3s (ADR-0066: nada de mídia no
/// caminho do webhook).
/// </summary>
public sealed class SinalTranscricaoAudio
{
    private readonly Channel<bool> _canal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Sinalizar() => _canal.Writer.TryWrite(true);

    internal ChannelReader<bool> Leitor => _canal.Reader;
}

/// <summary>
/// Baixa pelo Zap os áudios recebidos no WhatsApp, transcreve no ElevenLabs e devolve o texto ao
/// fluxo normal de entrada. Acorda pelo sinal do webhook e, de qualquer forma, a cada meio minuto —
/// o que ficou para trás num reinício ou numa falha passageira é retomado sozinho.
/// </summary>
public sealed class TranscricaoAudioWorker(
    IServiceScopeFactory scopeFactory,
    SinalTranscricaoAudio sinal,
    ILogger<TranscricaoAudioWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);
    private const int PorPassagem = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processados;
                do
                {
                    using var scope = scopeFactory.CreateScope();
                    var servico = scope.ServiceProvider.GetRequiredService<IAudioTranscricaoConversaService>();
                    processados = await servico.ProcessarPendentesAsync(PorPassagem, stoppingToken);
                }
                while (processados == PorPassagem && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Transcrição de áudio do WhatsApp: passagem falhou.");
            }

            using var espera = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            espera.CancelAfter(Intervalo);
            try { await sinal.Leitor.ReadAsync(espera.Token); }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { /* intervalo */ }
            catch (OperationCanceledException) { return; }
        }
    }
}

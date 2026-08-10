using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Ser.Pacientes;

namespace SMSMarica.Core.Ser.Background;

/// <summary>
/// Leva ao hub FHIR os pacientes que a varredura carimbou como alterados.
///
/// <para><b>Por que é um worker e não um passo da varredura.</b> A rodada completa do SER leva
/// ~7 horas e passa por 25 mil solicitações. Conciliar dentro dela somaria milhares de
/// requisições ao hub e, sobretudo, faria uma indisponibilidade do hub <b>derrubar a varredura</b>
/// — a leitura do SER passaria a depender da saúde de outro serviço, sem nenhuma razão. Aqui os
/// dois são independentes: a varredura carimba, este worker leva, e se o hub estiver fora a fila
/// espera.</para>
///
/// <para><b>Ritmo deliberadamente lento.</b> Depois do alinhamento único a fila do dia a dia é de
/// dezenas, não de milhares — não há o que ganhar apertando o intervalo. Lotes pequenos também
/// deixam a escrita no hub diluída, em vez de concentrar rajadas.</para>
/// </summary>
public sealed class SerConciliacaoPacienteRunner(
    IServiceScopeFactory scopeFactory,
    ILogger<SerConciliacaoPacienteRunner> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    /// <summary>Teto por passagem — a fila continua na próxima, e não há pressa.</summary>
    private const int Lote = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Espera o serviço subir por completo antes da primeira passagem: no boot o pool de
        // conexões está em disputa com a migração e com o resto do startup.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var escopo = scopeFactory.CreateScope();
                var svc = escopo.ServiceProvider.GetRequiredService<ISerBackfillPacientesService>();
                await svc.ExecutarPendentesAsync(Lote, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Nada aqui pode matar o worker: a fila é durável e a próxima passagem retenta.
                logger.LogError(ex, "SER/conciliação: passagem falhou. Tentando de novo em {Min} min.",
                    Intervalo.TotalMinutes);
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

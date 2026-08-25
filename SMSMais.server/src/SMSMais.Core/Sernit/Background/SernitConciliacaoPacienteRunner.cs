using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Sernit.Pacientes;

namespace SMSMais.Core.Sernit.Background;

/// <summary>
/// Leva ao hub FHIR os pacientes que a varredura do SERNIT carimbou como alterados. Worker
/// independente da varredura (uma indisponibilidade do hub não pode derrubar a leitura do SERNIT):
/// a varredura carimba, este worker leva, e se o hub estiver fora a fila espera. Drena a fila
/// inteira a cada acordada, lote a lote, cada lote em seu próprio escopo (memória/change tracker).
/// </summary>
public sealed class SernitConciliacaoPacienteRunner(
    IServiceScopeFactory scopeFactory,
    ILogger<SernitConciliacaoPacienteRunner> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);
    private const int Lote = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                var total = 0;
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var escopo = scopeFactory.CreateScope();
                    var svc = escopo.ServiceProvider.GetRequiredService<ISernitBackfillPacientesService>();
                    var r = await svc.ExecutarPendentesAsync(Lote, stoppingToken);

                    if (r.Pacientes == 0) break;
                    total += r.Pacientes;

                    // Guarda de giro em falso: lote 100% falho voltaria idêntico — para e retoma depois.
                    if (r.Falhas == r.Pacientes)
                    {
                        logger.LogWarning(
                            "SERNIT/conciliação: lote inteiro falhou ({Qtd}). Parando a drenagem "
                            + "para não girar em falso; retoma na próxima passagem.", r.Falhas);
                        break;
                    }
                }

                if (total > 0)
                {
                    logger.LogInformation("SERNIT/conciliação: fila drenada — {Total} paciente(s).", total);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SERNIT/conciliação: passagem falhou. Tentando de novo em {Min} min.",
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

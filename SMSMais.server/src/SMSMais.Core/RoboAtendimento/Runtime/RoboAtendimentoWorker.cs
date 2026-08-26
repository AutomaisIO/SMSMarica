using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Runtime;

/// <summary>
/// Drena a fila <c>robo_tarefa</c> fora do caminho do webhook: pega as Pendentes elegíveis e
/// processa cada uma num escopo próprio (DbContext fresco). O handler só enfileira; a IA roda aqui.
/// </summary>
public sealed class RoboAtendimentoWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RoboAtendimentoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(5, configuration.GetValue("RoboAtendimento:IntervaloSegundos", 15)));
        var maximo = Math.Clamp(configuration.GetValue("RoboAtendimento:MaximoPorPassagem", 20), 1, 100);

        logger.LogInformation("RoboAtendimentoWorker iniciado (intervalo {Intervalo}s).", intervalo.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarPassagemAsync(maximo, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro na passagem do RoboAtendimentoWorker.");
            }

            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessarPassagemAsync(int maximo, CancellationToken ct)
    {
        List<Guid> ids;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
            var agora = DateTime.UtcNow;
            ids = await db.RoboTarefas.AsNoTracking()
                .Where(t => t.Status == StatusRoboTarefa.Pendente
                    && (t.ProximaTentativaEm == null || t.ProximaTentativaEm <= agora))
                .OrderBy(t => t.CriadoEm)
                .Take(maximo)
                .Select(t => t.Id)
                .ToListAsync(ct);
        }

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            await using var scope = scopeFactory.CreateAsyncScope();
            var proc = scope.ServiceProvider.GetRequiredService<IRoboAtendimentoProcessador>();
            await proc.ProcessarAsync(id, ct);
        }
    }
}

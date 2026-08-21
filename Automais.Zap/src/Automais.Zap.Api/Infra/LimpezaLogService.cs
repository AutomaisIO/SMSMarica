using Automais.Zap.Core.Relay;
using Automais.Zap.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Automais.Zap.Api.Infra;

/// <summary>
/// Expurga o <c>entrega_log</c> pela janela de retenção. É trilha operacional: manter para
/// sempre transformaria o relay num arquivo de metadados de todos os municípios, que é
/// justamente o que ele não deve ser.
/// </summary>
public sealed class LimpezaLogService(
    IServiceProvider provedor,
    IOptions<RelayOptions> opcoes,
    TimeProvider relogio,
    ILogger<LimpezaLogService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Folga no boot: não disputar com a migration nem com o primeiro tráfego.
        await Task.Delay(TimeSpan.FromMinutes(2), relogio, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dias = Math.Max(1, opcoes.Value.RetencaoLogDias);
                var corte = relogio.GetUtcNow().AddDays(-dias);

                await using var scope = provedor.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ZapDbContext>();

                var removidos = await db.EntregasLog
                    .Where(x => x.RecebidoEm < corte)
                    .ExecuteDeleteAsync(stoppingToken);

                if (removidos > 0)
                {
                    logger.LogInformation("Expurgo do entrega_log: {N} linhas anteriores a {Corte:o}.", removidos, corte);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Nunca derrubar o host por causa de faxina.
                logger.LogError(ex, "Falha no expurgo do entrega_log.");
            }

            await Task.Delay(TimeSpan.FromHours(6), relogio, stoppingToken);
        }
    }
}

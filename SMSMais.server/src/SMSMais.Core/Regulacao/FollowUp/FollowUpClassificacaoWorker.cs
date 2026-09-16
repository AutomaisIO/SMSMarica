using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Regulacao.FollowUp;

/// <summary>
/// Classifica em lote os FollowUPs que ainda não têm categoria, ou cuja categoria veio de regras
/// que já mudaram. Cobre o passado (backfill do que foi capturado antes da coluna existir) e o
/// futuro (edição de regras pela tela de configuração), sem passo manual.
///
/// <para>Fora da migration de propósito: as regras são configuração editável, não código, e um
/// backfill que varre duas tabelas grandes tem de rodar em lotes pequenos, não numa transação
/// de deploy. Cada lote tem o próprio escopo (change tracker curto).</para>
/// </summary>
public sealed class FollowUpClassificacaoWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<FollowUpClassificacaoWorker> logger) : BackgroundService
{
    private static readonly TimeSpan AtrasoInicial = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);
    private const int Lote = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(AtrasoInicial, stoppingToken);
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
                    var svc = escopo.ServiceProvider.GetRequiredService<IFollowUpClassificacaoService>();
                    var r = await svc.ClassificarPendentesAsync(Lote, stoppingToken);
                    if (r.Total == 0) break;
                    total += r.Total;

                    // Respiro entre lotes: o banco é compartilhado com outros produtos.
                    await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);
                }

                if (total > 0)
                {
                    logger.LogInformation("FollowUP: {Total} evento(s) classificado(s) nesta passagem.", total);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FollowUP: classificação em lote falhou. Tentando de novo em {Min} min.",
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Ser;
using SMSMais.Core.Sernit;

namespace SMSMais.Core.Regulacao.Notificacoes;

/// <summary>
/// Regra da limpeza automática das notificações de <b>Alta</b> no SER e no SERNIT (pedido de
/// 17/09/2026).
///
/// <para><b>Por quê:</b> Alta encerra o ciclo e não pede ação da regulação, mas ficava na fila até
/// alguém clicar em "Vista". Em 17/09 eram 73 no SER e 226 no SERNIT, quase todas com mais de 5
/// dias, e ninguém tinha marcado nenhuma no SERNIT. Esse ruído escondia o que precisa de reação.</para>
///
/// <para><b>A regra:</b> gatilho pendente cuja situação de destino é Alta e que foi criado há mais
/// de <see cref="Prazo"/> sai da fila. "Sem interação" quer dizer que ninguém marcou como vista:
/// abrir o detalhe não fica registrado, então não conta. Quem saiu assim fica carimbado com
/// <see cref="ProcessadoPor"/>, para distinguir da marcação feita por uma pessoa.</para>
/// </summary>
public static class LimpezaAltaNotificacao
{
    public static readonly TimeSpan Prazo = TimeSpan.FromDays(5);

    public const string ProcessadoPor = "Automático (Alta há mais de 5 dias)";
}

/// <summary>Roda a <see cref="LimpezaAltaNotificacao"/> de hora em hora. É um UPDATE só, com
/// índice pequeno (os pendentes): não precisa de lote.</summary>
public sealed class LimpezaAltaNotificacaoWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<LimpezaAltaNotificacaoWorker> logger) : BackgroundService
{
    private static readonly TimeSpan AtrasoInicial = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

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
                using var escopo = scopeFactory.CreateScope();
                var ser = await escopo.ServiceProvider.GetRequiredService<ISerNotificacaoService>()
                    .LimparAltasAntigasAsync(stoppingToken);
                var sernit = await escopo.ServiceProvider.GetRequiredService<ISernitNotificacaoService>()
                    .LimparAltasAntigasAsync(stoppingToken);

                if (ser + sernit > 0)
                {
                    logger.LogInformation(
                        "Notificações: {Ser} Alta(s) do SER e {Sernit} do SERNIT limpas automaticamente.",
                        ser, sernit);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notificações: limpeza automática de Alta falhou. Tentando de novo em {Min} min.",
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

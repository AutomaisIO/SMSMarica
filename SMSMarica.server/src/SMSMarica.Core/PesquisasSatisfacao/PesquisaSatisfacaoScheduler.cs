using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMarica.Core.PesquisasSatisfacao;

/// <summary>
/// Motor do disparo automático da pesquisa de satisfação.
///
/// <para><b>Nasce inerte.</b> Só age em unidade com o toggle ligado E link cadastrado — sem
/// isso o laço não faz nada além de acordar. Ligar é ato explícito na aba da unidade.</para>
///
/// <para>A marca d'água por unidade é o que torna o ciclo seguro: uma parada longa não faz
/// perder quem teve alta no intervalo, e a janela semiaberta impede que alguém na borda receba
/// dois convites. Somado ao índice único por atendimento, convidar duas vezes é impossível.</para>
/// </summary>
public sealed class PesquisaSatisfacaoScheduler(
    IServiceScopeFactory scopeFactory,
    ILogger<PesquisaSatisfacaoScheduler> logger) : BackgroundService
{
    /// <summary>
    /// Dez minutos: o convite já sai horas depois da alta, então precisão de minuto não muda
    /// nada para o paciente — e um laço curto só multiplicaria consulta ao hub à toa.
    /// </summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var servico = scope.ServiceProvider.GetRequiredService<IPesquisasSatisfacaoService>();
                var enviados = await servico.ProcessarGatilhoAsync(stoppingToken);
                if (enviados > 0)
                    logger.LogInformation("Pesquisa de satisfação: {N} convite(s) enviados.", enviados);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Falha de um ciclo não pode derrubar o motor: o próximo tenta de novo, e a marca
                // d'água garante que nada do intervalo se perdeu.
                logger.LogError(ex, "Falha no ciclo do disparo da pesquisa de satisfação.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

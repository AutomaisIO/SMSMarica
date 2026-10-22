using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.GuiasAgendamento;

/// <summary>
/// A fila de guias rodando (ADR-0076): uma linha que enfileira de tempos em tempos (só banco) e uma
/// linha POR SISTEMA de regulação que busca uma guia de cada vez, no ritmo (guias/hora) e na janela
/// configurados para aquele sistema — o SISREG devagar e de madrugada não segura o SER, e vice-versa.
/// Desligável inteira por configuração (<c>GuiasAgendamento:Habilitado=false</c>).
/// </summary>
public sealed class FilaGuiasScheduler(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuracao,
    ILogger<FilaGuiasScheduler> logger) : BackgroundService
{
    /// <summary>Os agendamentos aparecem em lotes (varreduras, importação do SISREG): 10 min põe a
    /// guia na fila logo depois de cada lote, sem custo que importe.</summary>
    private static readonly TimeSpan IntervaloEnfileirar = TimeSpan.FromMinutes(10);

    /// <summary>Fora da janela, desligada, fila vazia ou sessão ocupada: olhar de novo daqui a pouco.</summary>
    private static readonly TimeSpan Ociosa = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(configuracao["GuiasAgendamento:Habilitado"], "false", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Fila de guias desligada por configuração (GuiasAgendamento:Habilitado=false).");
            return;
        }

        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        SistemaRegulacao[] sistemas;
        using (var scope = scopeFactory.CreateScope())
        {
            sistemas = [.. scope.ServiceProvider.GetServices<IBaixadorGuia>().Select(b => b.Sistema).Distinct()];
        }

        await Task.WhenAll([
            RepetirAsync("enfileirar", IntervaloEnfileirar, async ct =>
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IFilaGuiasService>().EnfileirarAsync(ct);
            }, stoppingToken),
            .. sistemas.Select(s => BaixarAsync(s, stoppingToken)),
        ]);
    }

    /// <summary>
    /// A chave-mestra do sincronismo automático do SISREG também para a fila de guias do SISREG — ela
    /// é agenda automática, não clique de ninguém. Mora aqui porque só scheduler consulta a
    /// chave-mestra (<c>HistoricoManualTests</c>); a tela de configuração usa este mesmo método para
    /// dizer por que a fila parou.
    /// </summary>
    internal static async Task<string?> ChaveMestraAsync(SmsMaisDbContext db, SistemaRegulacao sistema, CancellationToken ct) =>
        sistema == SistemaRegulacao.Sisreg && !await SincronismoAutomaticoSisreg.LigadoAsync(db, ct)
            ? "O sincronismo automático do SISREG está desligado."
            : null;

    /// <summary>A linha de um sistema: uma guia, espera 3600/PorHora segundos, a próxima.</summary>
    private async Task BaixarAsync(SistemaRegulacao sistema, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var espera = Ociosa;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var configuracoes = scope.ServiceProvider.GetRequiredService<IFilaGuiasConfiguracaoService>();
                var config = await configuracoes.ObterAsync(sistema, stoppingToken);

                // A mesma régua que a tela mostra: desligada, pausada, fora da janela, sessão ocupada…
                if (await configuracoes.ImpedimentoAsync(config, stoppingToken) is null)
                {
                    var desfecho = await scope.ServiceProvider.GetRequiredService<IFilaGuiasService>()
                        .ProcessarProximaAsync(sistema, stoppingToken);
                    // Descartada não foi ao sistema: segue para a próxima sem esperar o ritmo.
                    espera = desfecho switch
                    {
                        DesfechoFilaGuia.FilaVazia => Ociosa,
                        DesfechoFilaGuia.Descartada => TimeSpan.Zero,
                        _ => TimeSpan.FromSeconds(3600.0 / Math.Max(1, config.PorHora)),
                    };
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Nunca deixa subir: BackgroundServiceExceptionBehavior=StopHost derrubaria a API.
                logger.LogError(ex, "Fila de guias do {Sistema}: falha na rodada — tenta de novo.", sistema);
            }

            try { await Task.Delay(espera, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task RepetirAsync(string nome, TimeSpan intervalo, Func<CancellationToken, Task> acao, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await acao(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fila de guias ({Nome}): falha — tenta de novo.", nome);
            }

            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}

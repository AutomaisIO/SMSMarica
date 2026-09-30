using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SMSMais.Core.Regulacao.AnaliseRegras;

/// <summary>Seção <c>Regulacao:AnaliseEspelho</c>. Ligada por padrão; <c>Ativo=false</c> no env do
/// servidor (<c>Regulacao__AnaliseEspelho__Ativo=false</c>) desliga sem deploy.</summary>
public sealed class AnaliseRegrasEspelhoOpcoes
{
    public const string Secao = "Regulacao:AnaliseEspelho";

    public bool Ativo { get; set; } = true;

    public int IntervaloMinutos { get; set; } = 10;

    /// <summary>Teto por sistema por passada — a primeira passada do SER tem milhares de pedidos
    /// em aberto; divide em fatias para não segurar o banco.</summary>
    public int LimitePorPassada { get; set; } = 3000;
}

/// <summary>
/// Passa a análise de regras nos pedidos em aberto do SER, do SERNIT e do ESUS SG a cada 10 min
/// (ADR-0063 §4). Desacoplada das varreduras de propósito: a análise não pode derrubar nem atrasar
/// a leitura do sistema externo, e regra editada na tela é pega pelo hash na passada seguinte.
/// </summary>
public sealed class AnaliseRegrasEspelhoWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AnaliseRegrasEspelhoOpcoes> opcoes,
    ILogger<AnaliseRegrasEspelhoWorker> logger) : BackgroundService
{
    private static readonly TimeSpan AtrasoInicial = TimeSpan.FromMinutes(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = opcoes.Value;
        if (!o.Ativo)
        {
            logger.LogWarning("Análise de regras dos espelhos DESLIGADA ({Secao}:Ativo=false).", AnaliseRegrasEspelhoOpcoes.Secao);
            return;
        }

        try
        {
            await Task.Delay(AtrasoInicial, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, o.IntervaloMinutos)));
        do
        {
            foreach (var sistema in AnaliseRegrasEspelhoService.Cobertos)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var servico = scope.ServiceProvider.GetRequiredService<IAnaliseRegrasEspelhoService>();
                    await servico.AnalisarPendentesAsync(sistema, Math.Max(1, o.LimitePorPassada), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Análise de regras do espelho {Sistema} falhou nesta passada.", sistema);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Data;

namespace SMSMais.Core.Rastreamento.Biometria;

/// <summary>
/// Retenção das selfies das conferências do motorista (ADR-0073 §8): passados
/// <c>Tfd:Biometria:DiasSelfie</c> dias (padrão 90), a foto sai e o registro fica, com a data em que
/// a selfie foi apagada. A foto de referência não entra aqui: ela vive enquanto o motorista existir.
/// </summary>
public sealed class LimpezaSelfiesWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<LimpezaSelfiesWorker> logger) : BackgroundService
{
    private static readonly TimeSpan AtrasoInicial = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(AtrasoInicial, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
                var apagadas = await ApagarVencidasAsync(db, Dias(config), DateTime.UtcNow, stoppingToken);
                if (apagadas > 0)
                    logger.LogInformation("Biometria: {Apagadas} selfies de conferência apagadas pela retenção.", apagadas);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Falha de um ciclo não derruba a rotina; o próximo tenta de novo.
                logger.LogError(ex, "Biometria: falha na limpeza das selfies vencidas.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    public static int Dias(IConfiguration config) => Math.Max(1, config.GetValue("Tfd:Biometria:DiasSelfie", 90));

    public static Task<int> ApagarVencidasAsync(SmsMaisDbContext db, int dias, DateTime agora, CancellationToken ct)
    {
        var limite = agora.AddDays(-dias);
        return db.VerificacoesFaciaisMotorista
            .Where(v => v.SelfieBase64 != null && v.RecebidaEm < limite)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.SelfieBase64, (string?)null)
                .SetProperty(v => v.SelfieApagadaEm, agora), ct);
    }
}

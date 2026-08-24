using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Integracoes.Pep.Divergencias;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.Pep.Background;

/// <summary>
/// Arbitragem de divergências de identidade (ADR-0039) como job PRÓPRIO, fora do run de
/// sincronismo.
///
/// <para><b>Por que saiu de dentro do run.</b> Na primeira execução em produção (02/08) a
/// arbitragem rodava como fase final do sincronismo e o segurou por mais de 11 minutos: quem
/// está do outro lado é um serviço externo, pago e de latência imprevisível. Amarrar o
/// fechamento de um ciclo de importação — que já leva horas de Oracle e hub — à
/// disponibilidade de um fornecedor de consulta de CPF é acoplamento errado: o sincronismo
/// não deveria nem atrasar nem falhar por causa disso.</para>
///
/// <para>Aqui as duas coisas correm em cadências independentes. Se o fornecedor estiver fora,
/// a fila de divergências apenas não anda — o sincronismo segue normal, e as divergências
/// continuam <b>congelando</b> o campo em disputa (que é a proteção que importa).</para>
///
/// <para><b>Respeita a pausa do motor</b>: quando o operador pausa a base — tipicamente porque
/// algo está errado —, a arbitragem daquela base também para. Ninguém quer queimar consulta
/// paga durante um incidente.</para>
/// </summary>
public sealed class VerificadorDivergenciasScheduler(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<VerificadorDivergenciasScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(
            Math.Clamp(configuration.GetValue("Pep:Divergencias:IntervaloMinutos", 15), 1, 24 * 60));
        var max = configuration.GetValue("Pep:Divergencias:MaxPorRodada", 50);
        var teto = TimeSpan.FromSeconds(
            Math.Clamp(configuration.GetValue("Pep:Divergencias:TetoSegundos", 120), 10, 3600));

        logger.LogInformation(
            "VerificadorDivergenciasScheduler iniciado (a cada {Int}, até {Max} por rodada, teto de {Teto}).",
            intervalo, max, teto);

        using var timer = new PeriodicTimer(intervalo);
        try
        {
            // Não arbitra no boot: dá tempo de o serviço estabilizar e evita rajada em restart.
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RodadaAsync(max, teto, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown normal
        }
    }

    private async Task RodadaAsync(int max, TimeSpan teto, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();

            // Fontes com fila pendente. Consulta barata — na esmagadora maioria dos ticks
            // não há nada a fazer e o job termina aqui, sem tocar em serviço externo.
            var fontesComFila = await db.PepDivergenciasIdentidade
                .Where(d => d.Status == StatusDivergenciaIdentidade.Pendente
                         || d.Status == StatusDivergenciaIdentidade.NaoConclusiva)
                .Select(d => d.FonteId)
                .Distinct()
                .ToListAsync(ct);
            if (fontesComFila.Count == 0) return;

            var agora = DateTime.UtcNow;
            var pausadas = await db.PepSincronizacaoAgendas.AsNoTracking()
                .Where(a => fontesComFila.Contains(a.FonteId) && a.PausadoAte != null && a.PausadoAte > agora)
                .Select(a => a.FonteId)
                .ToListAsync(ct);

            var verificador = scope.ServiceProvider.GetRequiredService<IVerificadorDivergenciasPep>();

            foreach (var fonteId in fontesComFila)
            {
                ct.ThrowIfCancellationRequested();
                if (pausadas.Contains(fonteId))
                {
                    logger.LogDebug("Arbitragem da fonte {Fonte} pulada — motor pausado.", fonteId);
                    continue;
                }

                var r = await verificador.VerificarPendentesAsync(fonteId, max, teto, ct);
                if (r.Analisadas == 0) continue;

                logger.LogInformation(
                    "Arbitragem da fonte {Fonte}: {N} analisadas (origem {O} · hub {H} · CPF suspeito {A} · inconclusivas {I}); restam {R}.",
                    fonteId, r.Analisadas, r.OrigemCorreta, r.HubCorreto, r.AmbosNegados, r.NaoConclusivas, r.Restantes);

                // Fornecedor fora do ar: não adianta insistir nas outras fontes nesta rodada.
                if (r.InterrompidaPorIndisponibilidade)
                {
                    logger.LogWarning(
                        "Consulta de CPF indisponível — rodada de arbitragem encerrada; retoma no próximo tick.");
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Job acessório: nunca derruba o host nem interfere no sincronismo.
            logger.LogError(ex, "Rodada de arbitragem de divergências falhou.");
        }
    }
}

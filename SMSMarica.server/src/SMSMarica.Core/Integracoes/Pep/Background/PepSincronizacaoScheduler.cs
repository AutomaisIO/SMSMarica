using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Integracoes.Pep.Progresso;
using SMSMarica.Data;

namespace SMSMarica.Core.Integracoes.Pep.Background;

/// <summary>
/// Scheduler do sincronismo contínuo (ADR-0024): a cada tick avalia as agendas ativas
/// (<c>pep_sincronizacao_agenda</c>) e enfileira execuções INCREMENTAIS pelas barreiras já
/// existentes (fila cap. 1 + estado vivo). Run manual sempre vence — o agendado que colide
/// apenas reprograma para daqui a pouco. O backoff pós-falha é aplicado pelo próprio
/// orquestrador ao finalizar a execução (ver <see cref="PepSincronizacaoService"/>).
/// </summary>
public sealed class PepSincronizacaoScheduler(
    IServiceScopeFactory scopeFactory,
    PepSincronizacaoEstadoVivo estadoVivo,
    IConfiguration configuration,
    ILogger<PepSincronizacaoScheduler> logger) : BackgroundService
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tick = TimeSpan.FromSeconds(Math.Max(15, configuration.GetValue("Pep:Agenda:TickSegundos", 60)));
        logger.LogInformation("PepSincronizacaoScheduler iniciado (tick {Tick}s).", tick.TotalSeconds);
        using var timer = new PeriodicTimer(tick);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await TickAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown normal
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();
            var servico = scope.ServiceProvider.GetRequiredService<IPepSincronizacaoService>();

            var agendas = await db.PepSincronizacaoAgendas.Where(a => a.Ativo).ToListAsync(ct);
            if (agendas.Count == 0) return;

            var agora = DateTime.UtcNow;
            var horaBrt = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(agora, Brasilia));
            var mudou = false;

            foreach (var agenda in agendas)
            {
                var decisao = DecididorAgendaPep.Decidir(agenda, agora, estadoVivo.ObterAtual() is not null, horaBrt);
                if (decisao != DecisaoAgenda.Disparar)
                {
                    if (decisao == DecisaoAgenda.PularRunVivo)
                        logger.LogDebug("Agenda {Fonte}: run vivo — pulando este tick.", agenda.FonteId);
                    continue;
                }

                var estado = await db.PepSincronizacaoEstados.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.FonteId == agenda.FonteId, ct);
                var forcarMedicos = DecididorAgendaPep.DeveForcarMedicos(
                    estado?.UltimoSyncMedicoEm, agenda.MedicoRescanHoras, agora);

                var execucaoId = await servico.IniciarAgendadoAsync(agenda.FonteId, forcarMedicos, ct);
                // Colisão/indisponibilidade → tenta de novo no próximo tick; sucesso → o
                // próximo disparo normal é reprogramado já (o pós-run só ajusta em falha).
                agenda.ProximoRunEm = execucaoId is null
                    ? agora.AddMinutes(1)
                    : DecididorAgendaPep.ProximoAposSucesso(agenda, agora);
                agenda.AtualizadoEm = agora;
                mudou = true;

                if (execucaoId is { } id)
                    logger.LogInformation("Agenda {Fonte}: execução incremental {Execucao} enfileirada (médicos: {Medicos}).",
                        agenda.FonteId, id, forcarMedicos ? "re-scan" : "pulado");
            }

            if (mudou) await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tick do scheduler de sincronização PEP falhou.");
        }
    }
}

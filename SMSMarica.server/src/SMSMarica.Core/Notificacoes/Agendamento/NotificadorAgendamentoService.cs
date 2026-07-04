using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Notificacoes.Agendamento;

/// <summary>
/// Worker da fila de notificação de agendamentos (mesmo padrão do EnviadorWorklistService):
/// a cada N segundos pega notificações Pendentes com <c>ProximaTentativaEm</c> vencida e
/// delega a <see cref="IAgendamentoNotificacaoService.ProcessarTentativaEnvioAsync"/> —
/// cada item isolado, uma falha não bloqueia a passagem.
/// </summary>
public sealed class NotificadorAgendamentoService(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificadorAgendamentoOptions> options,
    ILogger<NotificadorAgendamentoService> logger)
    : BackgroundService
{
    private readonly NotificadorAgendamentoOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Habilitado)
        {
            logger.LogInformation("NotificadorAgendamentoService desabilitado por configuração.");
            return;
        }

        var intervalo = TimeSpan.FromSeconds(Math.Max(5, _options.IntervaloSegundos));
        logger.LogInformation("NotificadorAgendamentoService iniciado — intervalo {Intervalo}.", intervalo);

        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecutarUmaPassagemAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha na passagem do NotificadorAgendamentoService — vai tentar de novo.");
            }

            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ExecutarUmaPassagemAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();
        var servico = scope.ServiceProvider.GetRequiredService<IAgendamentoNotificacaoService>();

        var agora = DateTime.UtcNow;
        var max = Math.Clamp(_options.MaximoPorPassagem, 1, 100);

        var pendentes = await db.AgendamentoNotificacoes.AsNoTracking()
            .Where(n => n.Status == StatusNotificacaoAgendamento.Pendente
                        && n.ProximaTentativaEm != null
                        && n.ProximaTentativaEm <= agora)
            .OrderBy(n => n.ProximaTentativaEm)
            .Take(max)
            .Select(n => n.Id)
            .ToListAsync(ct);

        if (pendentes.Count == 0) return;

        logger.LogDebug("Notificador processando {N} notificações de agendamento.", pendentes.Count);

        foreach (var id in pendentes)
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                await servico.ProcessarTentativaEnvioAsync(id, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado ao processar notificação {Id}.", id);
            }
        }
    }
}

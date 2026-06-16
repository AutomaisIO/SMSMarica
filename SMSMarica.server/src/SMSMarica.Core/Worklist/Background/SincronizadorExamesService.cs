using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Worklist.Background;

/// <summary>
/// Hosted service que pesquisa periodicamente no PACS (QIDO-RS) se as
/// solicitações no estado Recebida/EmExecucao já têm study lá. Quando acha,
/// promove para Realizada e dispara o notificador.
/// </summary>
public sealed class SincronizadorExamesService(
    IServiceScopeFactory scopeFactory,
    IOptions<SincronizadorExamesOptions> options,
    ILogger<SincronizadorExamesService> logger)
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly SincronizadorExamesOptions _options = options.Value;
    private readonly ILogger<SincronizadorExamesService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(5, _options.IntervaloSegundos));
        _logger.LogInformation("SincronizadorExamesService iniciado — intervalo {Intervalo}.", intervalo);

        // Pequeno delay inicial pra não atropelar o startup da API.
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
                _logger.LogError(ex, "Falha na passagem do SincronizadorExamesService — vai tentar de novo.");
            }

            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ExecutarUmaPassagemAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaStudyClient>();
        var solicitacoes = scope.ServiceProvider.GetRequiredService<ISolicitacoesExameService>();

        var corte = DateTime.UtcNow.AddDays(-Math.Max(1, _options.JanelaConsultaDias));

        var ativas = await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.ExcluidoEm == null
                        && (s.Status == StatusSolicitacaoExame.Recebida || s.Status == StatusSolicitacaoExame.EmExecucao)
                        && s.CriadoEm >= corte)
            .Select(s => new { s.Id, s.AccessionNumber })
            .ToListAsync(ct);

        if (ativas.Count == 0) return;

        _logger.LogDebug("Sincronizador verificando {N} solicitações ativas.", ativas.Count);

        foreach (var item in ativas)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                var existe = await consulta.StudyExisteAsync(item.AccessionNumber, ct);
                if (existe)
                {
                    await solicitacoes.MarcarComoRealizadaAsync(item.Id, DateTime.UtcNow, ct);
                    _logger.LogInformation(
                        "Solicitação {Accession} promovida para Realizada (study detectado no PACS).",
                        item.AccessionNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Falha ao checar/promover solicitação {Id} ({Accession}).", item.Id, item.AccessionNumber);
            }
        }
    }
}

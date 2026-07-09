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
/// Worker que processa o fluxo resiliente de envio Solicitada → Enviada → Agendada.
///
/// A cada N segundos pega solicitações cujo <c>ProximaTentativaEm</c> está
/// vencido e chama <see cref="ISolicitacoesExameService.ProcessarTentativaEnvioAsync"/>,
/// que decide entre POST (Solicitada→Enviada) e GET de confirmação
/// (Enviada→Agendada). Tudo idempotente e isolado por solicitação — uma
/// falha não bloqueia as outras.
/// </summary>
public sealed class EnviadorWorklistService(
    IServiceScopeFactory scopeFactory,
    IOptions<EnviadorWorklistOptions> options,
    ILogger<EnviadorWorklistService> logger)
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly EnviadorWorklistOptions _options = options.Value;
    private readonly ILogger<EnviadorWorklistService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(5, _options.IntervaloSegundos));
        _logger.LogInformation("EnviadorWorklistService iniciado — intervalo {Intervalo}.", intervalo);

        // Pequeno delay inicial pra não atropelar o startup.
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecutarUmaPassagemAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return; // shutdown real do host — encerra o worker
            }
            catch (Exception ex)
            {
                // Inclui TaskCanceledException de TIMEOUT do HttpClient (dcm4chee lento): trata
                // como falha transitória e continua o loop. NUNCA deixa a exceção subir — senão o
                // BackgroundServiceExceptionBehavior=StopHost derruba a API inteira.
                _logger.LogError(ex, "Falha na passagem do EnviadorWorklistService — vai tentar de novo.");
            }

            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ExecutarUmaPassagemAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();
        var solicitacoes = scope.ServiceProvider.GetRequiredService<ISolicitacoesExameService>();

        var agora = DateTime.UtcNow;
        var max = Math.Clamp(_options.MaximoPorPassagem, 1, 200);

        // Pega só os IDs primeiro, ordenado por urgência (proxima vencida primeiro).
        var pendentes = await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.ExcluidoEm == null
                        && (s.Status == StatusSolicitacaoExame.Solicitada
                            || s.Status == StatusSolicitacaoExame.Enviada)
                        && s.TipoExame!.EnviarParaWorklist // tipo com envio ao worklist desligado fica de fora
                        && s.ProximaTentativaEm != null
                        && s.ProximaTentativaEm <= agora)
            .OrderBy(s => s.ProximaTentativaEm)
            .Take(max)
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (pendentes.Count == 0) return;

        _logger.LogDebug("Enviador processando {N} solicitações pendentes.", pendentes.Count);

        foreach (var id in pendentes)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                await solicitacoes.ProcessarTentativaEnvioAsync(id, ct);
            }
            catch (Exception ex)
            {
                // ProcessarTentativaEnvioAsync já trata as exceções internamente
                // (atualiza ErroIntegracaoPacs + backoff). Isso aqui é só
                // defesa pra não derrubar a passagem.
                _logger.LogError(ex, "Erro inesperado ao processar tentativa de {Id}.", id);
            }
        }
    }
}

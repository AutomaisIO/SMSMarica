using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Associacoes;
using SMSMais.Core.Common.Tempo;

namespace SMSMais.Core.Worklist.Background;

/// <summary>
/// Hosted service que concilia os exames do PACS com as solicitações. É <b>PACS-driven</b>:
/// itera os studies que CHEGARAM (varridos por <c>StudyDate</c>, intrinsecamente recente) e
/// casa cada um com a solicitação pelo <b>AccessionNumber</b> — a chave durável do vínculo.
/// A data de CRIAÇÃO da solicitação é irrelevante: ela pode ter entrado a qualquer momento.
/// <para>
/// Isso substitui o modelo antigo (enumerar solicitações por <c>CriadoEm</c> e perguntar ao
/// PACS), que sofria <i>starvation</i> — um lote de solicitações novas abertas empurrava os
/// exames legítimos para fora do teto por passagem — e ignorava pedidos antigos.
/// </para>
/// A conciliação de cada study fica em <see cref="IExameAssociacaoService.ConciliarStudyAsync"/>.
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
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return; // shutdown real do host — encerra o worker
            }
            catch (Exception ex)
            {
                // Inclui TaskCanceledException de TIMEOUT do HttpClient (PACS lento): trata como
                // falha transitória e continua o loop. NUNCA deixa a exceção subir — senão o
                // BackgroundServiceExceptionBehavior=StopHost derruba a API inteira.
                _logger.LogError(ex, "Falha na passagem do SincronizadorExamesService — vai tentar de novo.");
            }

            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ExecutarUmaPassagemAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaStudyClient>();
        var associacao = scope.ServiceProvider.GetRequiredService<IExameAssociacaoService>();

        // Janela pela DATA DO EXAME (StudyDate). +1 dia de folga cobre a borda de fuso
        // (StudyDate é wall-clock de Brasília; o servidor roda em UTC).
        var hojeBr = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var inicio = hojeBr.AddDays(-Math.Max(0, _options.JanelaConsultaDias));
        var fim = hojeBr.AddDays(1);

        var max = Math.Max(1, _options.MaximoPorPassagem);
        var estudos = await consulta.BuscarStudiesPorDataAsync(inicio, fim, max, ct);
        if (estudos.Count == 0) return;

        // Truncar sem avisar reintroduziria starvation silenciosa (a razão desta reescrita).
        if (estudos.Count >= max)
            _logger.LogWarning(
                "Sincronizador: janela {Ini}..{Fim} atingiu o teto de {Max} studies — parte da janela NÃO foi varrida; reduza JanelaConsultaDias ou aumente MaximoPorPassagem.",
                inicio, fim, max);

        _logger.LogDebug("Sincronizador: {N} studies no PACS na janela {Ini}..{Fim}.", estudos.Count, inicio, fim);

        // Pré-filtro em lote + conciliação por study (falha pontual não derruba a passagem).
        await associacao.ConciliarLoteAsync(estudos, ct);
    }
}

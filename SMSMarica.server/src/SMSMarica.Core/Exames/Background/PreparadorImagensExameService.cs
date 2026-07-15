using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Exames.Background;

public sealed class PreparadorImagensOptions
{
    public const string SecaoConfig = "PreparadorImagens";

    /// <summary>Liga/desliga o worker (o cache continua sendo preenchido on-demand).</summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>Intervalo entre passagens. Default 45s.</summary>
    public int IntervaloSegundos { get; set; } = 45;

    /// <summary>Máximo de exames preparados por passagem (SEQUENCIAL — nunca rajada no PACS).</summary>
    public int MaximoPorPassagem { get; set; } = 3;

    /// <summary>Tentativas antes de desistir de um exame (ex.: DICOM sumido do bucket).</summary>
    public int MaxTentativas { get; set; } = 5;

    /// <summary>Só prepara exames realizados nos últimos N dias — o histórico antigo
    /// continua on-demand (e entra no cache no primeiro acesso).</summary>
    public int JanelaDias { get; set; } = 7;
}

/// <summary>
/// PRÉ-MATERIALIZA o PDF de imagens do exame assim que ele fica pronto no PACS
/// (Realizada/Laudada): aquece o cache de imagens rasterizadas
/// (<c>render-cache/{study}.zip</c>, via <see cref="IExamePacsImagensReader"/>) e o PDF
/// do cidadão (<c>imagens-exame/{paciente}/{study}.pdf</c>) numa tacada só, chamando
/// <see cref="IExameImagensPdfService.GerarOuObterAsync"/>. Um exame por vez, de fundo —
/// no momento do clique (painel ou app do cidadão) o PDF sai do S3 sem tocar o dcm4chee,
/// que é quem sofre rasterizando DICOMs de ~53MB sob rajada de cliques.
/// </summary>
public sealed class PreparadorImagensExameService(
    IServiceScopeFactory scopeFactory,
    IOptions<PreparadorImagensOptions> options,
    ILogger<PreparadorImagensExameService> logger) : BackgroundService
{
    private readonly PreparadorImagensOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Habilitado)
        {
            logger.LogInformation("PreparadorImagensExameService desabilitado por configuração.");
            return;
        }

        var intervalo = TimeSpan.FromSeconds(Math.Max(10, _options.IntervaloSegundos));
        logger.LogInformation("PreparadorImagensExameService iniciado — intervalo {Intervalo}.", intervalo);

        // Delay inicial generoso: deixa o startup (e os workers mais críticos) respirarem.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
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
                // Inclui TaskCanceledException de TIMEOUT do HttpClient (PACS lento): falha
                // transitória, continua o loop. NUNCA deixa subir (StopHost derrubaria a API).
                logger.LogError(ex, "Falha na passagem do PreparadorImagensExameService — vai tentar de novo.");
            }

            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ExecutarUmaPassagemAsync(CancellationToken ct)
    {
        List<Guid> pendentes;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();
            var corte = DateTime.UtcNow.AddDays(-Math.Max(1, _options.JanelaDias));
            var max = Math.Clamp(_options.MaximoPorPassagem, 1, 20);

            // Recentes primeiro: são os que os pacientes vão clicar assim que o zap chega.
            pendentes = await db.ExamesImagem.AsNoTracking()
                .Where(s => s.ExcluidoEm == null
                            && (s.Status == StatusSolicitacaoExame.Realizada
                                || s.Status == StatusSolicitacaoExame.Laudada)
                            && s.ImagensPreparadasEm == null
                            && s.ImagensPreparacaoTentativas < _options.MaxTentativas
                            && s.RealizadoEm != null
                            && s.RealizadoEm >= corte)
                .OrderByDescending(s => s.RealizadoEm)
                .Take(max)
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        if (pendentes.Count == 0) return;
        logger.LogDebug("Preparador: {N} exame(s) para pré-materializar.", pendentes.Count);

        // Estritamente SEQUENCIAL: é o throttle que protege o dcm4chee.
        foreach (var id in pendentes)
        {
            if (ct.IsCancellationRequested) return;
            await PrepararUmAsync(id, ct);
        }
    }

    private async Task PrepararUmAsync(Guid solicitacaoId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();
        var pdfImagens = scope.ServiceProvider.GetRequiredService<IExameImagensPdfService>();

        var sol = await db.ExamesImagem
            .FirstOrDefaultAsync(s => s.Id == solicitacaoId && s.ExcluidoEm == null, ct);
        if (sol is null) return;

        try
        {
            // Aquece o render-cache (reader) E materializa o PDF do cidadão de uma vez.
            // O PDF do "exame completo" (painel) reaproveita o mesmo render-cache no clique.
            await pdfImagens.GerarOuObterAsync(solicitacaoId, ct);

            sol.ImagensPreparadasEm = DateTime.UtcNow;
            logger.LogInformation(
                "Preparador: exame {Pedido} pré-materializado (tentativa {N}).",
                sol.AccessionNumber, sol.ImagensPreparacaoTentativas + 1);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return; // shutdown no meio da preparação — nem conta tentativa
        }
        catch (Exception ex)
        {
            sol.ImagensPreparacaoTentativas++;
            logger.LogWarning(ex,
                "Preparador: falha ao pré-materializar o exame {Pedido} (tentativa {N}/{Max}).",
                sol.AccessionNumber, sol.ImagensPreparacaoTentativas, _options.MaxTentativas);
        }

        await db.SaveChangesAsync(ct);
    }
}

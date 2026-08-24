using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Exames.Background;

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

    // ---- Re-validação: conserta exames cujo estudo CRESCEU no PACS após a materialização
    //      (envio parcial do equipamento seguido de reenvio). Ver ticket #68. ----

    /// <summary>Liga/desliga a re-validação de imagens (o reparo on-demand continua valendo).</summary>
    public bool RevalidacaoHabilitada { get; set; } = true;

    /// <summary>Janela (HORAS) de exames re-verificados. Cobre reenvios tardios do equipamento.
    /// Alargar temporariamente (ex.: 1440 = 60 dias) faz uma varredura histórica única.</summary>
    public int RevalidacaoJanelaHoras { get; set; } = 72;

    /// <summary>Após N horas do exame sem o estudo crescer, considera-se estável e para de re-verificar
    /// (evita re-ler S3/PACS de exames já íntegros).</summary>
    public int RevalidacaoEstavelHoras { get; set; } = 24;

    /// <summary>Intervalo entre passagens de re-verificação.</summary>
    public int RevalidacaoIntervaloSegundos { get; set; } = 300;

    /// <summary>Máximo de exames re-verificados por passagem (SEQUENCIAL — throttle do PACS/S3).</summary>
    public int RevalidacaoMaxPorPassagem { get; set; } = 10;
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

    // Exames já confirmados íntegros (cache == PACS e estudo estável): não re-verifica de novo.
    // Só o loop de fundo (single-thread) acessa — sem concorrência. Limpo na saída da janela.
    private readonly HashSet<Guid> _estaveis = [];
    private DateTime _ultimaRevalidacao = DateTime.MinValue;

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

                if (_options.RevalidacaoHabilitada
                    && DateTime.UtcNow - _ultimaRevalidacao
                       >= TimeSpan.FromSeconds(Math.Max(30, _options.RevalidacaoIntervaloSegundos)))
                {
                    await RevalidarUmaPassagemAsync(stoppingToken);
                    _ultimaRevalidacao = DateTime.UtcNow;
                }
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
            var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
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
        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
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

    // ---------------- Re-validação (ticket #68): estudo que cresceu no PACS após materializado ----------------

    private async Task RevalidarUmaPassagemAsync(CancellationToken ct)
    {
        List<(Guid Id, DateTime? RealizadoEm)> candidatos;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
            var corte = DateTime.UtcNow.AddHours(-Math.Max(1, _options.RevalidacaoJanelaHoras));

            candidatos = await db.ExamesImagem.AsNoTracking()
                .Where(s => s.ExcluidoEm == null
                            && (s.Status == StatusSolicitacaoExame.Realizada
                                || s.Status == StatusSolicitacaoExame.Laudada)
                            && s.ImagensPreparadasEm != null
                            && s.RealizadoEm != null
                            && s.RealizadoEm >= corte)
                .OrderByDescending(s => s.RealizadoEm)
                .Select(s => new ValueTuple<Guid, DateTime?>(s.Id, s.RealizadoEm))
                .ToListAsync(ct);
        }

        // Poda o skip-set: tira quem saiu da janela (mantém o set pequeno).
        var idsNaJanela = candidatos.Select(c => c.Id).ToHashSet();
        _estaveis.RemoveWhere(id => !idsNaJanela.Contains(id));

        var pendentes = candidatos.Where(c => !_estaveis.Contains(c.Id)).ToList();
        if (pendentes.Count == 0) return;

        var max = Math.Clamp(_options.RevalidacaoMaxPorPassagem, 1, 50);
        var estavelCorte = DateTime.UtcNow.AddHours(-Math.Max(1, _options.RevalidacaoEstavelHoras));

        var processados = 0;
        foreach (var (id, realizadoEm) in pendentes)
        {
            if (ct.IsCancellationRequested) return;
            if (processados >= max) break;
            processados++;
            await RevalidarUmAsync(id, realizadoEm, estavelCorte, ct);
        }
    }

    private async Task RevalidarUmAsync(Guid id, DateTime? realizadoEm, DateTime estavelCorte, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
        var pdfImagens = scope.ServiceProvider.GetRequiredService<IExameImagensPdfService>();

        var r = await pdfImagens.ReavaliarAsync(id, ct);

        if (r.Defasado)
        {
            // O cache serviu MENOS imagens do que o PACS tem: o estudo foi completado (reenvio) depois.
            // Invalida os dois caches e regenera com o conjunto COMPLETO.
            await pdfImagens.InvalidarAsync(id, ct);
            await pdfImagens.GerarOuObterAsync(id, ct);

            var sol = await db.ExamesImagem.FirstOrDefaultAsync(s => s.Id == id && s.ExcluidoEm == null, ct);
            if (sol is not null)
            {
                sol.ImagensPreparadasEm = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            logger.LogInformation(
                "Re-validação: exame {Id} reprocessado — cache {De} -> PACS {Para} imagens (estudo completado após materialização).",
                id, r.ImagensCache, r.ImagensPacs);
            return;
        }

        // Cache bate com o PACS. Se o exame já é antigo o bastante (sem crescer), marca estável e
        // para de re-verificar — até lá segue sendo re-checado (janela em que um reenvio ainda chega).
        if (r.ImagensCache is not null && r.ImagensPacs > 0
            && r.ImagensCache == r.ImagensPacs
            && realizadoEm is { } rz && rz <= estavelCorte)
        {
            _estaveis.Add(id);
        }
    }
}

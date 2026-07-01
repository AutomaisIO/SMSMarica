using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMarica.Core.Associacoes;
using SMSMarica.Core.Associacoes.Dtos;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Worklist.Background;

/// <summary>
/// Hosted service que pesquisa periodicamente no PACS (QIDO-RS) os exames das
/// solicitações abertas e os concilia:
/// <list type="number">
/// <item>worklist: o study com o nosso AccessionNumber já está no PACS → promove a Realizada;</item>
/// <item>sem worklist: o exame chegou com o NÚMERO DA SOLICITAÇÃO no campo Patient ID
/// (0010,0020) → auto-associa o study à solicitação (e promove a Realizada).</item>
/// </list>
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
        var associacao = scope.ServiceProvider.GetRequiredService<IExameAssociacaoService>();

        var corte = DateTime.UtcNow.AddDays(-Math.Max(1, _options.JanelaConsultaDias));

        // Solicitações ABERTAS (não concluídas) na janela. Inclui Solicitada porque
        // exames sem worklist nunca passam por Enviada/Recebida.
        var ativas = await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.ExcluidoEm == null
                        && (s.Status == StatusSolicitacaoExame.Solicitada
                            || s.Status == StatusSolicitacaoExame.Enviada
                            || s.Status == StatusSolicitacaoExame.Recebida
                            || s.Status == StatusSolicitacaoExame.EmExecucao)
                        && s.CriadoEm >= corte)
            .OrderByDescending(s => s.CriadoEm)
            .Take(Math.Max(1, _options.MaximoPorPassagem)) // teto de carga QIDO por passagem
            .Select(s => new { s.Id, s.AccessionNumber, s.StudyInstanceUID, s.Status })
            .ToListAsync(ct);

        if (ativas.Count == 0) return;

        // Não re-escaneia o que já tem associação explícita ativa.
        var ids = ativas.Select(a => a.Id).ToList();
        var jaAssociadas = (await db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.ExcluidoEm == null && ids.Contains(a.SolicitacaoExameId))
            .Select(a => a.SolicitacaoExameId)
            .ToListAsync(ct)).ToHashSet();

        _logger.LogDebug("Sincronizador verificando {N} solicitações abertas.", ativas.Count);

        foreach (var item in ativas)
        {
            if (ct.IsCancellationRequested) return;
            if (jaAssociadas.Contains(item.Id)) continue;

            try
            {
                // 1) Caminho worklist: study com o nosso AccessionNumber já no PACS.
                if (item.Status is StatusSolicitacaoExame.Recebida or StatusSolicitacaoExame.EmExecucao
                    && await consulta.StudyExisteAsync(item.AccessionNumber, ct))
                {
                    // Data/hora REAL do exame vem do DICOM (StudyDate/StudyTime) — fonte da
                    // verdade. Falha do PACS aqui não pode quebrar a sincronização: em erro,
                    // dataEstudo fica null e a exibição faz fallback para RealizadoEm.
                    var dataEstudo = await ResolverDataEstudoAsync(consulta, item.StudyInstanceUID, item.AccessionNumber, ct);
                    await solicitacoes.MarcarComoRealizadaAsync(item.Id, DateTime.UtcNow, dataEstudo, ct);
                    _logger.LogInformation(
                        "Solicitação {Accession} promovida para Realizada (study via worklist).", item.AccessionNumber);
                    continue;
                }

                // 2) Caminho automático: exame chegou com o nº da solicitação no campo
                //    Patient ID (0010,0020). Roda para QUALQUER status aberto (não só
                //    Solicitada): com a worklist da máquina desligada/ignorada, a solicitação
                //    pode ter avançado para Enviada/Recebida e o exame chegar depois — esses
                //    não se recuperariam se barrássemos por status. Seguro: em exame de
                //    worklist real o Patient ID é o CPF, então a busca por accession não casa
                //    (no-op). Custo: +1 QIDO por solicitação aberta/passagem (limitado por
                //    MaximoPorPassagem; some assim que a associação é criada).
                var encontrados = await consulta.BuscarPorPatientIdAsync(item.AccessionNumber, ct);
                if (encontrados.Count > 1)
                {
                    _logger.LogWarning(
                        "Auto-associação: {N} estudos com Patient ID {Accession} — associando todos; verifique aquisição duplicada.",
                        encontrados.Count, item.AccessionNumber);
                }
                foreach (var estudo in encontrados)
                {
                    if (string.Equals(estudo.StudyInstanceUID, item.StudyInstanceUID, StringComparison.Ordinal))
                        continue; // é o próprio study de worklist (tratado no caminho 1)
                    try
                    {
                        await associacao.AssociarAsync(
                            new AssociarExameRequest(estudo.StudyInstanceUID, item.AccessionNumber, estudo.AccessionNumber),
                            OrigemAssociacaoExame.Automatica, validarNoPacs: false, ct);
                        _logger.LogInformation(
                            "Auto-associação: study {Uid} ligado à solicitação {Accession} via Patient ID.",
                            estudo.StudyInstanceUID, item.AccessionNumber);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex,
                            "Falha ao auto-associar study {Uid} à {Accession}.", estudo.StudyInstanceUID, item.AccessionNumber);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Falha ao processar solicitação {Id} ({Accession}).", item.Id, item.AccessionNumber);
            }
        }
    }

    /// <summary>
    /// Resolve a data/hora real do exame (StudyDate/StudyTime) via QIDO-RS, blindado:
    /// nenhuma exceção do PACS escapa (retorna null e a exibição faz fallback).
    /// </summary>
    private async Task<DateTime?> ResolverDataEstudoAsync(
        IConsultaStudyClient consulta, string studyInstanceUID, string accession, CancellationToken ct)
    {
        try
        {
            return await consulta.ObterDataHoraEstudoAsync(studyInstanceUID, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Falha ao obter StudyDate/StudyTime do DICOM para {Accession} — segue sem DataEstudo.", accession);
            return null;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.Confirmacoes;

/// <summary>
/// Lembrete X dias antes do agendamento. A primeira mensagem sai quando o agendamento entra (na
/// importação); esta sai às vésperas, que é quando a pessoa de fato se organiza — ou descobre que
/// não vai poder ir, ainda a tempo de a vaga ser remanejada.
///
/// <para>Dois modelos, pela resposta que a pessoa já deu: quem <b>confirmou</b> recebe o lembrete
/// objetivo; quem <b>não respondeu</b> recebe o que ainda pede confirmação. Quem já avisou que
/// <b>não vai</b> não recebe nada — seria pedir de novo o que ela já respondeu.</para>
///
/// <para><b>Silêncio mínimo:</b> quem recebeu a mensagem principal há menos de
/// <see cref="ComunicacaoPacienteOptions.LembreteIntervaloMinimoDias"/> dias não recebe lembrete.
/// Avisar de novo quem acabou de ser avisado é insistência — e insistência faz bloquear o número.</para>
///
/// <para>Só ENFILEIRA. Quem envia é o <see cref="EnviadorComunicacaoService"/>, com a mesma janela
/// de horário, a mesma vazão e as mesmas regras de LGPD (contato negado, contato verificado).</para>
/// </summary>
public interface ILembreteAgendamentoService
{
    /// <summary>Enfileira os lembretes devidos agora. Devolve quantos entraram na fila.</summary>
    Task<int> EnfileirarDevidosAsync(CancellationToken ct = default);
}

public sealed class LembreteAgendamentoService(
    SmsMaisDbContext db,
    IConfirmacaoConfiguracaoService regras,
    IComunicacaoPacienteService comunicacoes,
    IOptions<ComunicacaoPacienteOptions> options,
    ILogger<LembreteAgendamentoService> logger) : ILembreteAgendamentoService
{
    /// <summary>Teto por passagem: o lembrete é diário e previsível — rajada aqui é sinal de erro.</summary>
    private const int MaximoPorPassagem = 500;

    public async Task<int> EnfileirarDevidosAsync(CancellationToken ct = default)
    {
        var cfg = await regras.ObterAsync(ct);
        if (!cfg.LembreteHabilitado) return 0;

        var agora = DateTime.UtcNow;
        var limite = agora.AddDays(cfg.LembreteDiasAntes);
        var silencio = agora.AddDays(-Math.Max(0, options.Value.LembreteIntervaloMinimoDias));

        // Unidades e procedimentos com aviso ligado — a MESMA régua da primeira mensagem: quem não
        // avisa na entrada não passa a avisar na véspera.
        var unidadesLigadas = await db.SisregVarreduraAgendas.AsNoTracking()
            .Where(a => a.EnviarConfirmacao)
            .Select(a => a.UnidadeId)
            .ToListAsync(ct);
        if (unidadesLigadas.Count == 0) return 0;

        var codigosLigados = (await db.SisregProcedimentosProfissional.AsNoTracking()
                .Where(p => p.Profissional != null
                    && unidadesLigadas.Contains(p.Profissional.UnidadeId)
                    && p.EnviarConfirmacao)
                .Select(p => new { p.Profissional!.UnidadeId, p.Codigo })
                .Distinct()
                .ToListAsync(ct))
            .GroupBy(x => x.UnidadeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Codigo).ToHashSet(StringComparer.Ordinal));

        // Quem já avisou que não vai fica de fora; quem confirmou e quem não respondeu entram —
        // com modelos diferentes (resolvidos no envio, pelo status daquele momento).
        var candidatos = await db.Solicitacoes
            .Where(s => s.ExcluidoEm == null
                && s.Status != StatusSolicitacao.Cancelada
                && unidadesLigadas.Contains(s.UnidadeExecutanteId)
                && s.DataAgendada > agora && s.DataAgendada <= limite
                && s.StatusConfirmacao != StatusConfirmacaoAgendamento.Cancelada
                && !db.ComunicacoesPaciente.Any(c =>
                    c.SolicitacaoId == s.Id && c.Finalidade == FinalidadeComunicacao.LembreteAgendamento)
                // Falou com essa pessoa faz pouco tempo? Então não fala de novo.
                && !db.ComunicacoesPaciente.Any(c =>
                    c.SolicitacaoId == s.Id
                    && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                    && c.EnviadoEm != null && c.EnviadoEm > silencio))
            .OrderBy(s => s.DataAgendada)
            .Take(MaximoPorPassagem)
            .ToListAsync(ct);

        var enfileirados = 0;
        foreach (var s in candidatos)
        {
            if (cfg.SomenteSisreg && !OrigemAgendamento.EhDoSisreg(s)) continue;

            var codigo = s.ProcedimentoCodigoSisreg?.Trim();
            var ligados = codigosLigados.GetValueOrDefault(s.UnidadeExecutanteId);
            if (string.IsNullOrEmpty(codigo) || ligados is null || !ligados.Contains(codigo)) continue;

            await comunicacoes.EnfileirarAsync(s, FinalidadeComunicacao.LembreteAgendamento, ct);
            enfileirados++;
        }

        if (enfileirados > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Lembrete de agendamento: {Qtd} enfileirado(s) (janela de {Dias} dia(s)).",
                enfileirados, cfg.LembreteDiasAntes);
        }
        return enfileirados;
    }
}

/// <summary>
/// Varre a agenda de tempos em tempos e enfileira os lembretes devidos. Separado do enviador de
/// propósito: enfileirar é barato e pode rodar a qualquer hora; ENVIAR é que respeita a janela.
/// </summary>
public sealed class LembreteAgendamentoWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<LembreteAgendamentoWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ILembreteAgendamentoService>()
                    .EnfileirarDevidosAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Nunca deixa subir: BackgroundServiceExceptionBehavior=StopHost derrubaria a API.
                logger.LogError(ex, "Falha ao enfileirar lembretes de agendamento — tenta de novo.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}

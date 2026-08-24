using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Data;

namespace SMSMarica.Core.Integracoes.SisregWeb.Varredura.Background;

/// <summary>
/// Dispara a varredura diária de cada unidade na hora configurada. Espelha o scheduler do
/// sincronismo de PEP (ADR-0024): <c>PeriodicTimer</c>, escopo de DI por tick, decisão em código
/// puro e testável.
///
/// <para><b>Uma unidade por tick, no máximo.</b> Todas saem para o SISREG pelo mesmo IP, então
/// disparar várias de uma vez só aproximaria o CAPTCHA — quem ficou mais tempo sem varrer vai
/// primeiro, e as outras pegam os ticks seguintes.</para>
/// </summary>
public sealed class VarreduraSisregScheduler(
    IServiceScopeFactory scopeFactory,
    VarreduraSisregEstadoVivo estadoVivo,
    Importacao.Background.SisregImportacaoEstadoVivo importacaoEstadoVivo,
    IOptions<VarreduraSisregOpcoes> opcoes,
    ILogger<VarreduraSisregScheduler> logger) : BackgroundService
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly VarreduraSisregOpcoes _opcoes = opcoes.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(15, _opcoes.TickSegundos));
        using var timer = new PeriodicTimer(intervalo);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Um tick ruim não pode derrubar o host nem parar os próximos.
                logger.LogError(ex, "Erro no tick do scheduler de varredura SISREG.");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // Barato e evita abrir escopo de DI à toa: se já há trabalho vivo, nem consulta o banco.
        if (estadoVivo.ObterAtual() is not null || importacaoEstadoVivo.ObterAtual() is not null) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();

        var agora = DateTime.UtcNow;
        var horaLocal = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(agora, Brasilia));

        // Dentro do bloqueio do expo_solicitacoes nem vale consultar: nenhuma unidade poderia disparar.
        if (!DecididorVarreduraSisreg.PodeIniciar(horaLocal, _opcoes.CorteEntradaLocal, _opcoes.BloqueioFimLocal))
            return;

        // Quem ficou mais tempo sem varrer vai primeiro. Quando o orçamento não cobre a rede toda
        // num dia, isso vira rodízio justo em vez de umas unidades nunca serem varridas.
        var candidatas = await db.SisregVarreduraAgendas
            .Where(a => a.Ativo && a.ProximoRunEm != null && a.ProximoRunEm <= agora)
            .OrderBy(a => a.UltimaExecucaoEm == null ? 0 : 1)
            .ThenBy(a => a.UltimaExecucaoEm)
            .Take(10)
            .ToListAsync(ct);

        foreach (var agenda in candidatas)
        {
            var decisao = DecididorVarreduraSisreg.Decidir(
                agenda, agora, horaLocal, _opcoes.CorteEntradaLocal, _opcoes.BloqueioFimLocal,
                varreduraViva: estadoVivo.ObterAtual() is not null,
                importacaoViva: importacaoEstadoVivo.ObterAtual() is not null);

            if (decisao != DecisaoVarredura.Disparar) continue;

            var servico = scope.ServiceProvider.GetRequiredService<IVarreduraAgendaService>();
            var execucaoId = await servico.IniciarAgendadoAsync(agenda.UnidadeId, ct);

            if (execucaoId is null)
            {
                // Colisão não é erro: só tenta de novo no próximo tick.
                agenda.ProximoRunEm = agora.AddMinutes(1);
            }
            else
            {
                agenda.ProximoRunEm = DecididorVarreduraSisreg.ProximoDiario(agenda.HoraLocal, agora, Brasilia);
                logger.LogInformation(
                    "SISREG_VARREDURA_AGENDADA: unidade {UnidadeId} disparada, execução {ExecucaoId}.",
                    agenda.UnidadeId, execucaoId);
            }

            agenda.AtualizadoEm = agora;
            await db.SaveChangesAsync(ct);

            // Uma por tick: o IP é compartilhado.
            break;
        }
    }
}

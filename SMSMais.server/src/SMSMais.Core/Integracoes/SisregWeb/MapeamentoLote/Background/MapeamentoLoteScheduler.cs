using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Background;

namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Background;

/// <summary>
/// Dispara o lote de sincronização do mapeamento (profissionais/procedimentos + FHIR) uma vez por
/// dia, na hora configurada (flag do #118). Espelha o <c>VarreduraSisregScheduler</c>:
/// <c>PeriodicTimer</c>, decisão simples, escopo de DI por tick.
///
/// <para>Não há janela de bloqueio de horário aqui: o mapeamento usa o <c>sisreg_ajax</c>, que não
/// sofre o bloqueio 08:00–15:00 do <c>expo_solicitacoes</c> da agenda. Ainda assim, o padrão é a
/// madrugada, para não competir com o operador humano pelo orçamento anti-robô.</para>
/// </summary>
public sealed class MapeamentoLoteScheduler(
    IServiceScopeFactory scopeFactory,
    MapeamentoLoteEstadoVivo estadoVivo,
    VarreduraSisregEstadoVivo varreduraEstadoVivo,
    SisregImportacaoEstadoVivo importacaoEstadoVivo,
    IOptions<SisregMapeamentoLoteOpcoes> opcoes,
    ILogger<MapeamentoLoteScheduler> logger) : BackgroundService
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>Janela (min) depois da hora alvo em que ainda se dispara. Evita re-disparo e cobre
    /// um tick perdido, sem depender de estado persistido.</summary>
    private const double JanelaDisparoMinutos = 10;

    private readonly SisregMapeamentoLoteOpcoes _opcoes = opcoes.Value;

    /// <summary>Último dia (Brasília) em que o lote diário foi disparado — evita repetir no mesmo dia.</summary>
    private DateOnly? _ultimoDisparo;

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
                logger.LogError(ex, "Erro no tick do scheduler do lote de mapeamento SISREG.");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // Barato e evita abrir escopo de DI à toa: se já há trabalho vivo, nem consulta a config.
        if (estadoVivo.EmExecucao
            || varreduraEstadoVivo.ObterAtual() is not null
            || importacaoEstadoVivo.ObterAtual() is not null)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var servico = scope.ServiceProvider.GetRequiredService<ISisregMapeamentoLoteService>();

        var agendamento = await servico.ObterAgendamentoAsync(ct);
        if (!agendamento.Ativo) return;
        if (!TimeOnly.TryParseExact(agendamento.HoraLocal, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var hora))
        {
            return;
        }

        var agoraLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Brasilia);
        var hoje = DateOnly.FromDateTime(agoraLocal);
        if (_ultimoDisparo == hoje) return;

        var alvo = agoraLocal.Date + hora.ToTimeSpan();
        var minutosDesde = (agoraLocal - alvo).TotalMinutes;
        if (minutosDesde < 0 || minutosDesde >= JanelaDisparoMinutos) return;

        if (await servico.DispararAgendadoAsync(ct))
        {
            _ultimoDisparo = hoje;
            logger.LogInformation(
                "SISREG_MAPEAMENTO_LOTE_AGENDADO: lote diário disparado (hora alvo {Hora} Brasília).",
                agendamento.HoraLocal);
        }
        // Sem disparar (colisão ou sem unidades): tenta de novo nos próximos ticks dentro da janela.
    }
}

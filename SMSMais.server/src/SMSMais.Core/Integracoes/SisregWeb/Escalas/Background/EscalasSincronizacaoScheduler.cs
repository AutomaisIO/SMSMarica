using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.SisregWeb.Escalas.Background;

/// <summary>
/// Dispara o sincronismo de escalas uma vez por dia, na hora configurada. Espelha o
/// <c>MapeamentoLoteScheduler</c>: <c>PeriodicTimer</c>, decisão simples, escopo de DI por tick.
///
/// <para>Não há bloqueio de horário aqui — a tela <c>cons_escalas</c> não sofre a trava 08:00–15:00
/// do <c>expo_solicitacoes</c>. O padrão continua sendo madrugada por outro motivo: a sessão do
/// SISREG é única por operador, e rodar no expediente derruba quem está atendendo.</para>
/// </summary>
public sealed class EscalasSincronizacaoScheduler(
    IServiceScopeFactory scopeFactory,
    EscalasSincronizacaoEstadoVivo estadoVivo,
    Varredura.Background.VarreduraSisregEstadoVivo varreduraEstadoVivo,
    Importacao.Background.SisregImportacaoEstadoVivo importacaoEstadoVivo,
    MapeamentoLote.Background.MapeamentoLoteEstadoVivo loteEstadoVivo,
    IOptions<EscalasSincronizacaoOpcoes> opcoes,
    ILogger<EscalasSincronizacaoScheduler> logger) : BackgroundService
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly EscalasSincronizacaoOpcoes _opcoes = opcoes.Value;

    /// <summary>Último dia (Brasília) em que disparou — evita repetir dentro da janela.</summary>
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
                // Um tick ruim não pode derrubar o host nem parar os próximos.
                logger.LogError(ex, "Erro no tick do scheduler de escalas do SISREG.");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // Barato e evita abrir escopo de DI à toa: se já há trabalho vivo, nem consulta a config.
        if (estadoVivo.EmExecucao
            || varreduraEstadoVivo.ObterAtual() is not null
            || importacaoEstadoVivo.ObterAtual() is not null
            || loteEstadoVivo.EmExecucao)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();

        // Chave-mestra da tela de configuração: desligada, nada dispara sozinho.
        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
        if (!await SincronismoAutomaticoSisreg.LigadoAsync(db, ct)) return;

        var servico = scope.ServiceProvider.GetRequiredService<IEscalasSincronizacaoService>();
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
        if (minutosDesde < 0 || minutosDesde >= _opcoes.JanelaDisparoMinutos) return;

        if (await servico.DispararAgendadoAsync(ct))
        {
            _ultimoDisparo = hoje;
            logger.LogInformation(
                "SISREG_ESCALAS_AGENDADO: sincronismo diário disparado (hora alvo {Hora} Brasília).",
                agendamento.HoraLocal);
        }
        // Sem disparar (colisão ou orçamento): tenta de novo nos próximos ticks dentro da janela.
    }
}

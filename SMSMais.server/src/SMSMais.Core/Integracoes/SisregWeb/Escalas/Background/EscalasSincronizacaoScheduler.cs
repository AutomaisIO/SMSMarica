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

    /// <summary>
    /// Último <b>slot</b> (dia + horário de Brasília) em que disparou — evita repetir dentro da
    /// janela de disparo.
    ///
    /// <para>Era só o dia, quando havia um horário por dia. Com vários, guardar o dia faria o
    /// primeiro disparo bloquear todos os outros até a meia-noite: às 06:00 marcaria "hoje já
    /// rodou" e 12:00 e 18:00 nunca aconteceriam. O par (dia, horário) é o que distingue os
    /// slots.</para>
    /// </summary>
    private (DateOnly Dia, string Hora)? _ultimoDisparo;

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

        var agoraLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Brasilia);
        var hoje = DateOnly.FromDateTime(agoraLocal);

        // Slot vencido MAIS RECENTE, não o primeiro que casar: se o serviço ficou fora do ar entre
        // 06:00 e 18:00, disparar o slot das 06:00 agora traria o mesmo arquivo que o das 18:00 e
        // ainda deixaria o das 18:00 marcado como não rodado. O que interessa é o dado de agora.
        string? slot = null;
        foreach (var candidato in agendamento.HorariosLocais)
        {
            if (!TimeOnly.TryParseExact(candidato, "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var hora))
            {
                continue;
            }

            var minutosDesde = (agoraLocal - (agoraLocal.Date + hora.ToTimeSpan())).TotalMinutes;
            if (minutosDesde < 0 || minutosDesde >= _opcoes.JanelaDisparoMinutos) continue;
            slot = candidato;
        }

        if (slot is null) return;
        if (_ultimoDisparo == (hoje, slot)) return;

        if (await servico.DispararAgendadoAsync(ct))
        {
            _ultimoDisparo = (hoje, slot);
            logger.LogInformation(
                "SISREG_ESCALAS_AGENDADO: sincronismo disparado (slot {Hora} Brasília; horários do dia: {Todos}).",
                slot, string.Join(", ", agendamento.HorariosLocais));
        }
        // Sem disparar (colisão ou orçamento): tenta de novo nos próximos ticks dentro da janela.
    }
}

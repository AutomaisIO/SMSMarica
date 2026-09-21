using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Tempo;

namespace SMSMais.Core.Integracoes.SisregWeb.Cancelamento;

public sealed class ConciliacaoCancelamentosOpcoes
{
    public const string Secao = "Sisreg:ConciliacaoCancelamentos";

    /// <summary>
    /// Nasce DESLIGADA. Ligar é decisão de operação: o backfill de 20/09/2026 já pôs a base em dia
    /// até aquela data, e a partir daí cada passada é sobre o dia corrente.
    /// </summary>
    public bool Habilitado { get; set; }

    /// <summary>
    /// Minutos entre passadas. Dez, medido: um dia útil tem 46–62 cancelamentos, a listagem traz
    /// 20 por página, e o ciclo custa ~1 a 3 requisições (~1,5 s). A 10 minutos dá 11 req/h no dia
    /// típico e 60 req/h no pior dia já visto (371 cancelamentos, desligamento de profissional em
    /// massa) — contra um teto de ~700/h, numa faixa do dia em que hoje não há requisição nenhuma.
    /// Cinco minutos dobraria o custo para ganhar cinco minutos de latência que ninguém sente.
    /// </summary>
    public int IntervaloMinutos { get; set; } = 10;

    /// <summary>
    /// Janela de Brasília. Das 1.599 cancelações medidas em 31 dias, <b>98,2% caem entre 8h e 18h
    /// de segunda a sexta</b>; nada entre 22h e 6h. Fora da janela o SISREG fica em paz — e o que
    /// escapar é recolhido pela passada de fechamento.
    /// </summary>
    public int HoraInicio { get; set; } = 8;
    public int HoraFim { get; set; } = 18;

    /// <summary>
    /// Passada de fechamento: uma vez por dia, cedo, relendo o dia ANTERIOR. É o conferidor do que
    /// aconteceu fora da janela (as 29 de 1.599 que caíram às 7h, 19h, 21h e no fim de semana) e
    /// de qualquer passada que tenha falhado calada.
    /// </summary>
    public int HoraFechamento { get; set; } = 7;
}

/// <summary>
/// Roda a conciliação de cancelamentos a cada poucos minutos, dentro do expediente.
///
/// <para><b>Só lê.</b> Usa a credencial de sincronismo, como a varredura — não há operador humano
/// por trás de uma conciliação. Escrever no SISREG continua exigindo o login de quem clica.</para>
///
/// <para><b>Ocupa uma faixa hoje vazia.</b> A carga automática atual (~306 requisições/dia) é toda
/// noturna: varredura das 18h à 1h20, fila às 3h, escalas em três horários. Entre 7h e 18h o
/// SISREG não recebe nada nosso — que é justamente quando a rede cancela.</para>
/// </summary>
public sealed class ConciliacaoCancelamentosScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<ConciliacaoCancelamentosOpcoes> opcoes,
    ILogger<ConciliacaoCancelamentosScheduler> logger) : BackgroundService
{
    private readonly ConciliacaoCancelamentosOpcoes _opcoes = opcoes.Value;

    /// <summary>Dia do último fechamento, para não repeti-lo a cada tick da hora marcada.</summary>
    private DateOnly? _ultimoFechamento;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opcoes.Habilitado)
        {
            logger.LogInformation(
                "Conciliação de cancelamentos do SISREG: DESLIGADA "
                + $"({ConciliacaoCancelamentosOpcoes.Secao}:Habilitado).");
            return;
        }

        var intervalo = TimeSpan.FromMinutes(Math.Clamp(_opcoes.IntervaloMinutos, 1, 120));
        using var timer = new PeriodicTimer(intervalo);

        logger.LogInformation(
            "Conciliação de cancelamentos do SISREG: a cada {Min} min, das {Ini}h às {Fim}h "
            + "(fechamento do dia anterior às {Fech}h).",
            intervalo.TotalMinutes, _opcoes.HoraInicio, _opcoes.HoraFim, _opcoes.HoraFechamento);

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
                // Um tick ruim não pode derrubar o host nem impedir o próximo.
                logger.LogError(ex, "Erro no tick da conciliação de cancelamentos do SISREG.");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var agora = FusoBrasilia.ParaExibicao(DateTime.UtcNow);
        var hoje = DateOnly.FromDateTime(agora);

        // Fechamento: relê o dia anterior inteiro. Pega o que caiu fora da janela e o que uma
        // passada anterior tenha perdido — inclusive um domingo, na segunda de manhã.
        if (agora.Hour == _opcoes.HoraFechamento && _ultimoFechamento != hoje)
        {
            _ultimoFechamento = hoje;
            await ConciliarAsync(hoje.AddDays(-1), "fechamento", ct);
            return;
        }

        if (agora.Hour < _opcoes.HoraInicio || agora.Hour >= _opcoes.HoraFim) return;

        await ConciliarAsync(hoje, "expediente", ct);
    }

    private async Task ConciliarAsync(DateOnly dia, string motivo, CancellationToken ct)
    {
        using var escopo = scopeFactory.CreateScope();
        var servico = escopo.ServiceProvider.GetRequiredService<IConciliacaoCancelamentosSisregService>();

        var r = await servico.ConciliarDiaAsync(dia, ct);

        // Só fala quando há o que contar: um log por tick, dez vezes por hora, vira ruído que
        // esconde o log que importa.
        if (r.Conciliados > 0 || r.Aviso is not null)
        {
            logger.LogInformation(
                "Conciliação ({Motivo}) de {Dia}: {Novos} cancelamento(s) trazidos para a base.",
                motivo, dia, r.Conciliados);
        }
    }
}

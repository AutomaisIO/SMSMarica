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
    /// De quanto em quanto tempo o agendador ACORDA — não é a cadência da leitura, que fica no
    /// banco (menu Confirmações → Regras). Acordar de minuto em minuto e decidir ali é o que
    /// permite mudar a cadência sem reiniciar a API: um <c>PeriodicTimer</c> nasce com o intervalo
    /// fixo e só respeitaria o valor novo no próximo restart.
    /// </summary>
    public int TickSegundos { get; set; } = 60;
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

    /// <summary>
    /// Quando a última leitura do dia corrente saiu — é daqui que a cadência é medida.
    ///
    /// <para>Nasce com o instante do arranque, não nulo: um reinício não pode ANTECIPAR leitura.
    /// Com nulo, três deploys seguidos dentro de cinco minutos renderiam três conciliações extras,
    /// cada uma gastando do orçamento anti-robô. O agendador antigo adiava a primeira leitura em um
    /// intervalo inteiro; manter esse comportamento é o conservador.</para>
    /// </summary>
    private DateTime _ultimaLeituraUtc = DateTime.UtcNow;

    /// <summary>Quantas vezes o fechamento de hoje já falhou — para tentar de novo sem martelar.</summary>
    private (DateOnly Dia, int Tentativas) _falhasFechamento;

    /// <summary>
    /// Teto de dias que o fechamento recupera de uma vez. Sete cobre uma semana de API fora do ar
    /// sem transformar a volta em varredura de mês — cada dia custa uma passada inteira no SISREG.
    /// </summary>
    private const int MaximoDiasAtrasados = 7;

    /// <summary>Tentativas de fechamento por dia antes de deixar para o dia seguinte.</summary>
    private const int MaximoTentativasFechamento = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(Math.Clamp(_opcoes.TickSegundos, 15, 300)));

        logger.LogInformation(
            "Conciliação de cancelamentos do SISREG: agendador ativo. Cadência, janela e hora do "
            + "fechamento saem do menu Confirmações → Regras e valem sem reiniciar.");

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
        // TUDO mora no BANCO, com tela: ligar, cadência, janela e hora do fechamento. Lido a cada
        // tick — quem desliga às 14h quer que pare às 14h, e quem afrouxa a cadência quer que
        // afrouxe agora, não no próximo restart.
        Notificacoes.Confirmacoes.Dtos.ConfirmacaoConfiguracaoDto regras;
        using (var escopo = scopeFactory.CreateScope())
        {
            regras = await escopo.ServiceProvider
                .GetRequiredService<Notificacoes.Confirmacoes.IConfirmacaoConfiguracaoService>()
                .ObterAsync(ct);
        }
        if (!regras.ConciliacaoCancelamentoHabilitada) return;

        var agora = FusoBrasilia.ParaExibicao(DateTime.UtcNow);
        var hoje = DateOnly.FromDateTime(agora);

        // Fechamento: relê dias INTEIROS já encerrados. Pega o que caiu fora da janela e o que uma
        // passada tenha perdido — inclusive um domingo, na segunda de manhã.
        if (agora.Hour == regras.ConciliacaoHoraFechamento
            && await FecharDiasPendentesAsync(regras, hoje, ct))
            return;

        if (agora.Hour < regras.ConciliacaoHoraInicio || agora.Hour >= regras.ConciliacaoHoraFim) return;

        // A cadência é medida da última leitura, não do relógio: assim encurtar o intervalo passa
        // a valer no próximo tick, e alongar não deixa uma leitura pendurada.
        //
        // A tolerância de meio tick existe porque o tick nunca chega redondo: o próprio SELECT da
        // configuração custa milissegundos e varia. Sem ela, um tick que chega 40 ms curto é
        // descartado inteiro e a leitura só sai no tick seguinte — um intervalo de 1 minuto
        // entregaria 2, de forma intermitente e inexplicável para quem configurou.
        var intervalo = TimeSpan.FromMinutes(Math.Clamp(regras.ConciliacaoIntervaloMinutos, 1, 120));
        var tolerancia = TimeSpan.FromSeconds(Math.Clamp(_opcoes.TickSegundos, 15, 300) / 2.0);
        if (DateTime.UtcNow - _ultimaLeituraUtc < intervalo - tolerancia) return;

        _ultimaLeituraUtc = DateTime.UtcNow;
        await ConciliarAsync(hoje, "expediente", ct);
    }

    /// <summary>
    /// Fecha os dias encerrados que ainda não foram fechados, do mais antigo para o mais novo, e
    /// anota cada um <b>depois</b> de concluído. Devolve <c>true</c> quando trabalhou neste tick.
    ///
    /// <para>Três regras que custaram achado de revisão: (1) anotar só depois de a passada voltar
    /// COMPLETA — marcar antes fazia uma leitura truncada valer como dia fechado, e os
    /// cancelamentos daquele dia nunca entravam; (2) partir do último dia fechado GRAVADO, não de
    /// uma variável em memória, para um deploy em cima da hora marcada não sumir com o dia; (3) um
    /// dia por tick, para a recuperação de uma semana não virar sete passadas em rajada no
    /// SISREG.</para>
    /// </summary>
    private async Task<bool> FecharDiasPendentesAsync(
        Notificacoes.Confirmacoes.Dtos.ConfirmacaoConfiguracaoDto regras, DateOnly hoje, CancellationToken ct)
    {
        var ontem = hoje.AddDays(-1);
        var proximo = regras.ConciliacaoUltimoDiaFechado is { } ultimo
            ? ultimo.AddDays(1)
            : ontem;

        // Nunca mais que o teto para trás, e nunca um dia que ainda não acabou.
        if (proximo < ontem.AddDays(-MaximoDiasAtrasados)) proximo = ontem.AddDays(-MaximoDiasAtrasados);
        if (proximo > ontem) return false;

        // Desistir do dia depois de algumas tentativas evita martelar o SISREG de minuto em minuto
        // durante a hora inteira quando a sessão está caindo — a passada de amanhã tenta de novo,
        // porque a anotação do dia fechado só avança quando dá certo.
        if (_falhasFechamento.Dia == hoje && _falhasFechamento.Tentativas >= MaximoTentativasFechamento)
            return false;

        // O catch é o que faz a tentativa CONTAR. Sem ele a exceção subiria para o laço do
        // ExecuteAsync, o contador ficaria parado, e um SISREG fora do ar seria martelado de minuto
        // em minuto durante a hora inteira do fechamento.
        ConciliacaoCancelamentosDto? r;
        try
        {
            r = await ConciliarAsync(proximo, "fechamento", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Fechamento de {Dia} falhou.", proximo);
            r = null;
        }

        if (r is null || r.Aviso is not null)
        {
            _falhasFechamento = _falhasFechamento.Dia == hoje
                ? (hoje, _falhasFechamento.Tentativas + 1)
                : (hoje, 1);

            logger.LogWarning(
                "Fechamento de {Dia} não concluiu ({Tentativa}/{Maximo}): {Aviso}. O dia continua "
                + "pendente e será tentado de novo.",
                proximo, _falhasFechamento.Tentativas, MaximoTentativasFechamento,
                r?.Aviso ?? "a passada falhou");
            return true;
        }

        using var escopo = scopeFactory.CreateScope();
        await escopo.ServiceProvider
            .GetRequiredService<Notificacoes.Confirmacoes.IConfirmacaoConfiguracaoService>()
            .MarcarDiaFechadoAsync(proximo, ct);
        return true;
    }

    private async Task<ConciliacaoCancelamentosDto?> ConciliarAsync(
        DateOnly dia, string motivo, CancellationToken ct)
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

        return r;
    }
}

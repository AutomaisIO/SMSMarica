using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Integracoes.SerWeb.Varredura.Background;

/// <summary>Parâmetros do disparo automático (seção <c>Ser:Varredura</c> do appsettings).</summary>
public sealed class VarreduraSerOpcoes
{
    public const string Secao = "Ser:Varredura";

    /// <summary><b>Desligado por padrão.</b> Ligar o motor sozinho num ambiente novo derrubaria a
    /// sessão do operador do SER sem ninguém entender por quê — a sessão é única por operador.</summary>
    public bool Ativo { get; set; }

    /// <summary>
    /// Hora local de Brasília do disparo diário. De madrugada porque a rodada leva horas e ocupa a
    /// sessão do operador <c>147130107.gm</c> — rodar de dia tira o SER da mão de quem regula.
    /// </summary>
    public TimeOnly HoraLocal { get; set; } = new(2, 30);

    public int TickSegundos { get; set; } = 120;
}

/// <summary>
/// Dispara UMA rodada diária do motor do SER, no padrão do scheduler do SISREG.
///
/// <para><b>A trava do "já rodou hoje" é o banco, não a memória.</b> Um restart às 3h05 com o
/// controle em memória refaria a rodada inteira — horas de requisições contra a produção do Estado,
/// e a sessão do operador humano derrubada de novo. O critério é "existe execução com disparo
/// Agendado iniciada hoje (Brasília)".</para>
///
/// <para>Rodada <b>Diária</b>, nunca CargaInicial: a carga inicial relê o histórico de toda a base
/// (~50 min por 5.000 solicitações) e é decisão de operador, não de agenda.</para>
/// </summary>
public sealed class VarreduraSerScheduler(
    IVarreduraSerFila fila,
    IServiceScopeFactory scopeFactory,
    IOptions<VarreduraSerOpcoes> opcoes,
    ILogger<VarreduraSerScheduler> logger) : BackgroundService
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>A solicitação mais antiga vista em produção é de 2016; a janela começa antes disso
    /// para a varredura não deixar cauda para trás.</summary>
    private static readonly DateOnly InicioJanela = new(2015, 1, 1);

    private readonly VarreduraSerOpcoes _opcoes = opcoes.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opcoes.Ativo)
        {
            logger.LogInformation(
                "SER: disparo automático desligado (Ser:Varredura:Ativo=false). Só varredura manual.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30, _opcoes.TickSegundos)));

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
                logger.LogError(ex, "SER: erro no tick do scheduler de varredura.");
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        // Barato, e evita abrir escopo de DI à toa.
        if (fila.TemTrabalho) return;

        var agoraUtc = DateTime.UtcNow;
        var agoraLocal = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, Brasilia);
        if (TimeOnly.FromDateTime(agoraLocal) < _opcoes.HoraLocal) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();

        // Início do dia local, convertido para UTC — a coluna é timestamptz.
        var inicioDoDiaUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(agoraLocal.Date, DateTimeKind.Unspecified), Brasilia);

        var jaRodouHoje = await db.SerVarreduraExecucoes
            .AnyAsync(
                x => x.Disparo == DisparoSincronizacao.Agendado && x.IniciadoEm >= inicioDoDiaUtc,
                cancellationToken);

        if (jaRodouHoje) return;

        // Rodadas interrompidas são retomadas pelo runner na subida do serviço; se ainda houver uma
        // pendente, ela é a dona da sessão do SER e o disparo de hoje espera o próximo tick.
        var temPendente = await db.SerVarreduraExecucoes
            .AnyAsync(
                x => x.Status == StatusVarreduraSer.EmExecucao
                     || x.Status == StatusVarreduraSer.Pendente
                     || x.Status == StatusVarreduraSer.Interrompida,
                cancellationToken);

        if (temPendente)
        {
            logger.LogInformation(
                "SER: disparo diário adiado — ainda há rodada pendente ou interrompida em curso.");
            return;
        }

        var fim = DateOnly.FromDateTime(agoraLocal).AddDays(1);
        var enfileirou = fila.TentarEnfileirar(new PedidoVarreduraSer(
            ModoVarreduraSer.Diaria, DisparoSincronizacao.Agendado, InicioJanela, fim,
            null, null, null));

        logger.LogInformation(
            "SER: disparo diário {Resultado} (janela {Inicio:dd/MM/yyyy}..{Fim:dd/MM/yyyy}).",
            enfileirou ? "enfileirado" : "recusado (fila ocupada)", InicioJanela, fim);
    }
}

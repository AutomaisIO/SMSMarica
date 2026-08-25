using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Integracoes.SernitWeb.Varredura.Background;

/// <summary>Parâmetros do disparo automático do SERNIT (seção <c>Sernit:Varredura</c>).</summary>
public sealed class VarreduraSernitOpcoes
{
    public const string Secao = "Sernit:Varredura";

    /// <summary><b>Desligado por padrão.</b> Ligar o motor sozinho num ambiente novo derrubaria a
    /// sessão do operador do SERNIT (sessão única por operador).</summary>
    public bool Ativo { get; set; }

    public TimeOnly HoraLocal { get; set; } = new(2, 30);

    public int TickSegundos { get; set; } = 120;
}

/// <summary>
/// Dispara UMA rodada diária do motor do SERNIT (padrão do scheduler do SER-RJ/SISREG). A trava do
/// "já rodou hoje" é o BANCO, não a memória; a config (ativo/hora) é lida do banco a cada tick, com
/// o appsettings só como fallback de arranque. Rodada <b>Diária</b>, nunca CargaInicial.
/// </summary>
public sealed class VarreduraSernitScheduler(
    IVarreduraSernitFila fila,
    IServiceScopeFactory scopeFactory,
    IOptions<VarreduraSernitOpcoes> opcoes,
    ILogger<VarreduraSernitScheduler> logger) : BackgroundService
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>Janela ampla para não deixar cauda antiga para trás (IDs curtos do SERNIT já
    /// aparecem com data de solicitação de anos anteriores).</summary>
    private static readonly DateOnly InicioJanela = new(2015, 1, 1);

    private readonly VarreduraSernitOpcoes _opcoes = opcoes.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                logger.LogError(ex, "SERNIT: erro no tick do scheduler de varredura.");
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        if (fila.TemTrabalho) return;

        using var scope = scopeFactory.CreateScope();

        var config = await scope.ServiceProvider
            .GetRequiredService<Sernit.ISernitVarreduraConfigService>()
            .ObterAsync(cancellationToken);

        if (!config.Ativo) return;

        var hora = TimeOnly.TryParseExact(
            config.HoraLocal, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h)
            ? h
            : _opcoes.HoraLocal;

        var agoraUtc = DateTime.UtcNow;
        var agoraLocal = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, Brasilia);
        if (TimeOnly.FromDateTime(agoraLocal) < hora) return;

        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();

        var inicioDoDiaUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(agoraLocal.Date, DateTimeKind.Unspecified), Brasilia);

        var jaRodouHoje = await db.SernitVarreduraExecucoes
            .AnyAsync(
                x => x.Disparo == DisparoSincronizacao.Agendado && x.IniciadoEm >= inicioDoDiaUtc,
                cancellationToken);

        if (jaRodouHoje) return;

        var temPendente = await db.SernitVarreduraExecucoes
            .AnyAsync(
                x => x.Status == StatusVarreduraSernit.EmExecucao
                     || x.Status == StatusVarreduraSernit.Pendente
                     || x.Status == StatusVarreduraSernit.Interrompida,
                cancellationToken);

        if (temPendente)
        {
            logger.LogInformation(
                "SERNIT: disparo diário adiado — ainda há rodada pendente ou interrompida em curso.");
            return;
        }

        var fim = DateOnly.FromDateTime(agoraLocal).AddDays(1);
        var enfileirou = fila.TentarEnfileirar(new PedidoVarreduraSernit(
            ModoVarreduraSernit.Diaria, DisparoSincronizacao.Agendado, InicioJanela, fim,
            null, null, null));

        logger.LogInformation(
            "SERNIT: disparo diário {Resultado} (janela {Inicio:dd/MM/yyyy}..{Fim:dd/MM/yyyy}).",
            enfileirou ? "enfileirado" : "recusado (fila ocupada)", InicioJanela, fim);
    }
}

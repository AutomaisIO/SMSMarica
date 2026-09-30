using System.Globalization;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.EsusSg;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;

namespace SMSMais.Core.Integracoes.EsusSgWeb.Varredura.Background;

/// <summary>Um pedido de rodada do motor do ESUS SG.</summary>
public sealed record PedidoVarreduraEsusSg(
    ModoVarreduraEsusSg Modo,
    DisparoSincronizacao Disparo,
    DateOnly Inicio,
    DateOnly Fim,
    Guid? UsuarioId,
    string? UsuarioNome,
    Guid? ExecucaoParaRetomar = null);

/// <summary>Fila de capacidade 1 (uma rodada por vez — a sessão do ESUS é única). Mesmo contrato
/// da fila do SERNIT: <see cref="TentarEnfileirar"/> devolve false quando ocupada.</summary>
public interface IVarreduraEsusSgFila
{
    bool TentarEnfileirar(PedidoVarreduraEsusSg pedido);
    ValueTask<PedidoVarreduraEsusSg> LerAsync(CancellationToken cancellationToken);
    void Liberar();
    bool TemTrabalho { get; }
}

public sealed class VarreduraEsusSgFila : IVarreduraEsusSgFila
{
    private readonly Channel<PedidoVarreduraEsusSg> _canal =
        Channel.CreateBounded<PedidoVarreduraEsusSg>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
        });

    private int _ocupado;

    public bool TemTrabalho => Volatile.Read(ref _ocupado) == 1;

    public bool TentarEnfileirar(PedidoVarreduraEsusSg pedido)
    {
        if (Interlocked.CompareExchange(ref _ocupado, 1, 0) != 0) return false;
        if (_canal.Writer.TryWrite(pedido)) return true;
        Volatile.Write(ref _ocupado, 0);
        return false;
    }

    public ValueTask<PedidoVarreduraEsusSg> LerAsync(CancellationToken cancellationToken) =>
        _canal.Reader.ReadAsync(cancellationToken);

    public void Liberar() => Volatile.Write(ref _ocupado, 0);
}

/// <summary>Executa as rodadas enfileiradas; na subida, retoma a que o deploy/restart interrompeu.</summary>
public sealed class VarreduraEsusSgRunner(
    IVarreduraEsusSgFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<VarreduraEsusSgRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RetomarInterrompidasAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            PedidoVarreduraEsusSg pedido;
            try
            {
                pedido = await fila.LerAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var sincronizacao = scope.ServiceProvider.GetRequiredService<IEsusSgSincronizacaoService>();
                logger.LogInformation(
                    "ESUS SG: iniciando varredura {Modo} ({Disparo}) de {Inicio:dd/MM/yyyy} a {Fim:dd/MM/yyyy}.",
                    pedido.Modo, pedido.Disparo, pedido.Inicio, pedido.Fim);

                var id = await sincronizacao.ExecutarAsync(
                    pedido.Modo, pedido.Disparo, pedido.Inicio, pedido.Fim,
                    pedido.UsuarioId, pedido.UsuarioNome, pedido.ExecucaoParaRetomar, stoppingToken);

                logger.LogInformation("ESUS SG: varredura {Execucao} finalizada.", id);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("ESUS SG: varredura interrompida pelo desligamento do serviço.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ESUS SG: varredura falhou fora do controle do serviço.");
            }
            finally
            {
                fila.Liberar();
            }
        }
    }

    private async Task RetomarInterrompidasAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();

            var pendentes = await db.EsusSgVarreduraExecucoes
                .Where(x => x.Status == StatusVarreduraEsusSg.EmExecucao
                            || x.Status == StatusVarreduraEsusSg.Pendente
                            || x.Status == StatusVarreduraEsusSg.Interrompida)
                .OrderBy(x => x.IniciadoEm)
                .ToListAsync(cancellationToken);
            if (pendentes.Count == 0) return;

            foreach (var e in pendentes)
            {
                e.Status = StatusVarreduraEsusSg.Interrompida;
                e.MensagemErro = $"Interrompida pelo desligamento do serviço na fase {e.Fase}. "
                    + "Retomada automática a partir do ponteiro.";
            }
            await db.SaveChangesAsync(cancellationToken);

            var primeira = pendentes[0];
            var ok = fila.TentarEnfileirar(new PedidoVarreduraEsusSg(
                primeira.Modo, primeira.Disparo, primeira.JanelaInicio, primeira.JanelaFim,
                primeira.CriadoPor, primeira.CriadoPorNome, primeira.Id));

            logger.LogWarning(
                "ESUS SG: {Qtd} varredura(s) interrompida(s) na subida. Retomando {Execucao} na fase {Fase} "
                + "(enfileirada: {Ok}).", pendentes.Count, primeira.Id, primeira.Fase, ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ESUS SG: não foi possível retomar varreduras interrompidas na subida.");
        }
    }
}

/// <summary>Seção <c>EsusSg:Varredura</c> (opcional). O que vale de verdade é o que a tela grava
/// no banco (<see cref="IEsusSgVarreduraConfigService"/>); isto é só o relógio do tique.</summary>
public sealed class VarreduraEsusSgOpcoes
{
    public const string Secao = "EsusSg:Varredura";

    public TimeOnly HoraLocal { get; set; } = new(3, 0);

    public int TickSegundos { get; set; } = 120;
}

/// <summary>Dispara a rodada DIÁRIA no horário configurado na tela (uma por dia, de Brasília).</summary>
public sealed class VarreduraEsusSgScheduler(
    IVarreduraEsusSgFila fila,
    IServiceScopeFactory scopeFactory,
    IOptions<VarreduraEsusSgOpcoes> opcoes,
    ILogger<VarreduraEsusSgScheduler> logger) : BackgroundService
{
    private readonly VarreduraEsusSgOpcoes _opcoes = opcoes.Value;

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
                logger.LogError(ex, "ESUS SG: erro no tick do agendador de varredura.");
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        if (fila.TemTrabalho) return;

        using var scope = scopeFactory.CreateScope();
        var config = await scope.ServiceProvider.GetRequiredService<IEsusSgVarreduraConfigService>()
            .ObterAsync(cancellationToken);
        if (!config.Ativo) return;

        var hora = TimeOnly.TryParseExact(config.HoraLocal, "HH:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var h) ? h : _opcoes.HoraLocal;

        var agoraLocal = FusoBrasilia.ParaExibicao(DateTime.UtcNow);
        if (TimeOnly.FromDateTime(agoraLocal) < hora) return;

        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
        var inicioDoDiaUtc = FusoBrasilia.InicioDoDiaAtualEmUtc();

        if (await db.EsusSgVarreduraExecucoes.AnyAsync(
                x => x.Disparo == DisparoSincronizacao.Agendado && x.IniciadoEm >= inicioDoDiaUtc, cancellationToken))
        {
            return;
        }

        if (await db.EsusSgVarreduraExecucoes.AnyAsync(
                x => x.Status == StatusVarreduraEsusSg.EmExecucao
                     || x.Status == StatusVarreduraEsusSg.Pendente
                     || x.Status == StatusVarreduraEsusSg.Interrompida, cancellationToken))
        {
            logger.LogInformation("ESUS SG: disparo diário adiado — há rodada pendente ou interrompida em curso.");
            return;
        }

        var (inicio, fim) = EsusSgJanelas.Diaria(FusoBrasilia.HojeEmBrasilia());
        var ok = fila.TentarEnfileirar(new PedidoVarreduraEsusSg(
            ModoVarreduraEsusSg.Diaria, DisparoSincronizacao.Agendado, inicio, fim, null, null));

        logger.LogInformation("ESUS SG: disparo diário {Resultado} (agendados de {Inicio:dd/MM/yyyy} a {Fim:dd/MM/yyyy}).",
            ok ? "enfileirado" : "recusado (fila ocupada)", inicio, fim);
    }
}

/// <summary>As janelas de DATA DO AGENDAMENTO de cada modo.</summary>
public static class EsusSgJanelas
{
    /// <summary>A carga inicial começa aqui: antes de 2019 o ESUS não devolveu agendado de Maricá,
    /// e 2015 dá folga (mesmo início do SER/SERNIT).</summary>
    public static readonly DateOnly InicioHistorico = new(2015, 1, 1);

    /// <summary>Quanto à frente olhar. Um pedido que sai da fila com data a 8 meses precisa estar
    /// DENTRO da janela, senão a detecção de saída o acusaria de ter sumido.</summary>
    public const int DiasAFrente = 400;

    /// <summary>Quanto para trás na rodada diária: pega reagendamento para o passado recente e a
    /// resposta do paciente que chega depois do atendimento.</summary>
    public const int DiasParaTras = 45;

    public static (DateOnly Inicio, DateOnly Fim) Diaria(DateOnly hoje) =>
        (hoje.AddDays(-DiasParaTras), hoje.AddDays(DiasAFrente));

    public static (DateOnly Inicio, DateOnly Fim) CargaInicial(DateOnly hoje) =>
        (InicioHistorico, hoje.AddDays(DiasAFrente));
}

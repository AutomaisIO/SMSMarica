using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Data;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Integracoes.SernitWeb.Varredura.Background;

/// <summary>
/// Consome a fila e roda a varredura do SERNIT fora do ciclo da requisição (a rodada leva de
/// minutos a ~1 h, então nunca cabe dentro de um POST).
/// </summary>
public sealed class VarreduraSernitRunner(
    IVarreduraSernitFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<VarreduraSernitRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RetomarInterrompidasAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            PedidoVarreduraSernit pedido;
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
                var sincronizacao = scope.ServiceProvider.GetRequiredService<ISernitSincronizacaoService>();

                logger.LogInformation(
                    "SERNIT: iniciando varredura {Modo} ({Disparo}) de {Inicio:dd/MM/yyyy} a {Fim:dd/MM/yyyy}.",
                    pedido.Modo, pedido.Disparo, pedido.Inicio, pedido.Fim);

                var execucaoId = await sincronizacao.ExecutarAsync(
                    pedido.Modo, pedido.Disparo, pedido.Inicio, pedido.Fim, pedido.Situacoes,
                    pedido.UsuarioId, pedido.UsuarioNome, pedido.ExecucaoParaRetomar, stoppingToken);

                logger.LogInformation("SERNIT: varredura {Execucao} finalizada.", execucaoId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("SERNIT: varredura interrompida pelo desligamento do serviço.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SERNIT: varredura falhou fora do controle do serviço.");
            }
            finally
            {
                fila.Liberar();
            }
        }
    }

    /// <summary>Rodadas que ficaram <c>EmExecucao</c> quando o processo caiu são marcadas como
    /// <see cref="StatusVarreduraSernit.Interrompida"/> e a mais antiga é reenfileirada (a sessão é
    /// única por operador). O ponteiro diz onde continuar.</summary>
    private async Task RetomarInterrompidasAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();

            var pendentes = await db.SernitVarreduraExecucoes
                .Where(x => x.Status == StatusVarreduraSernit.EmExecucao
                            || x.Status == StatusVarreduraSernit.Pendente
                            || x.Status == StatusVarreduraSernit.Interrompida)
                .OrderBy(x => x.IniciadoEm)
                .ToListAsync(cancellationToken);

            if (pendentes.Count == 0) return;

            foreach (var execucao in pendentes)
            {
                execucao.Status = StatusVarreduraSernit.Interrompida;
                execucao.MensagemErro =
                    $"Interrompida pelo desligamento do serviço na fase {execucao.Fase}. "
                    + "Retomada automática a partir do ponteiro.";
            }
            await db.SaveChangesAsync(cancellationToken);

            var primeira = pendentes[0];
            var enfileirou = fila.TentarEnfileirar(new PedidoVarreduraSernit(
                primeira.Modo, primeira.Disparo, primeira.JanelaInicio, primeira.JanelaFim,
                null, primeira.CriadoPor, primeira.CriadoPorNome, primeira.Id));

            logger.LogWarning(
                "SERNIT: {Qtd} varredura(s) interrompida(s) na subida. Retomando {Execucao} na fase "
                + "{Fase} (enfileirada: {Ok}).",
                pendentes.Count, primeira.Id, primeira.Fase, enfileirou);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SERNIT: não foi possível retomar varreduras interrompidas na subida.");
        }
    }
}

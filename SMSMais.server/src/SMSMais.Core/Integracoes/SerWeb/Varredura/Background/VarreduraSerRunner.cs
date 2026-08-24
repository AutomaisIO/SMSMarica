using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Data;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Integracoes.SerWeb.Varredura.Background;

/// <summary>
/// Consome a fila e roda a varredura do SER fora do ciclo da requisição.
///
/// <para>A rodada completa leva de 15 min (só grade) a ~1 h (com histórico de toda a fila), então
/// ela nunca pode acontecer dentro de um POST — o cliente cairia por timeout muito antes e a
/// varredura ficaria órfã no meio.</para>
/// </summary>
public sealed class VarreduraSerRunner(
    IVarreduraSerFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<VarreduraSerRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RetomarInterrompidasAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            PedidoVarreduraSer pedido;
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
                var sincronizacao = scope.ServiceProvider.GetRequiredService<ISerSincronizacaoService>();

                logger.LogInformation(
                    "SER: iniciando varredura {Modo} ({Disparo}) de {Inicio:dd/MM/yyyy} a {Fim:dd/MM/yyyy}.",
                    pedido.Modo, pedido.Disparo, pedido.Inicio, pedido.Fim);

                var execucaoId = await sincronizacao.ExecutarAsync(
                    pedido.Modo, pedido.Disparo, pedido.Inicio, pedido.Fim, pedido.Situacoes,
                    pedido.UsuarioId, pedido.UsuarioNome, pedido.ExecucaoParaRetomar, stoppingToken);

                logger.LogInformation("SER: varredura {Execucao} finalizada.", execucaoId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("SER: varredura interrompida pelo desligamento do serviço.");
            }
            catch (Exception ex)
            {
                // O service já grava o erro na execução; aqui só garantimos que o runner
                // sobrevive para atender o próximo pedido.
                logger.LogError(ex, "SER: varredura falhou fora do controle do serviço.");
            }
            finally
            {
                fila.Liberar();
            }
        }
    }

    /// <summary>
    /// Rodadas que ficaram <c>EmExecucao</c> quando o processo caiu (deploy, restart, crash)
    /// são marcadas como <see cref="StatusVarreduraSer.Interrompida"/> e <b>reenfileiradas</b>.
    ///
    /// <para><b>Não são marcadas como erro.</b> A rodada leva horas; perdê-la por um deploy
    /// significaria refazer tudo, e marcá-la "concluída" faria o operador acreditar numa
    /// cobertura que não existe. O ponteiro (fase + situação + data + último IdSer) diz onde
    /// continuar, então a retomada custa só o que faltava.</para>
    /// </summary>
    private async Task RetomarInterrompidasAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();

            var pendentes = await db.SerVarreduraExecucoes
                .Where(x => x.Status == StatusVarreduraSer.EmExecucao
                            || x.Status == StatusVarreduraSer.Pendente
                            || x.Status == StatusVarreduraSer.Interrompida)
                .OrderBy(x => x.IniciadoEm)
                .ToListAsync(cancellationToken);

            if (pendentes.Count == 0) return;

            foreach (var execucao in pendentes)
            {
                execucao.Status = StatusVarreduraSer.Interrompida;
                execucao.MensagemErro =
                    $"Interrompida pelo desligamento do serviço na fase {execucao.Fase}. "
                    + "Retomada automática a partir do ponteiro.";
            }
            await db.SaveChangesAsync(cancellationToken);

            // Só a mais antiga volta para a fila: a sessão do SER é única por operador e duas
            // rodadas concorrentes se derrubariam. As demais continuam pendentes e entram depois.
            var primeira = pendentes[0];
            var enfileirou = fila.TentarEnfileirar(new PedidoVarreduraSer(
                primeira.Modo, primeira.Disparo, primeira.JanelaInicio, primeira.JanelaFim,
                null, primeira.CriadoPor, primeira.CriadoPorNome, primeira.Id));

            logger.LogWarning(
                "SER: {Qtd} varredura(s) interrompida(s) encontrada(s) na subida. Retomando {Execucao} "
                + "na fase {Fase} (enfileirada: {Ok}).",
                pendentes.Count, primeira.Id, primeira.Fase, enfileirou);
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode impedir o runner de subir.
            logger.LogError(ex, "SER: não foi possível retomar varreduras interrompidas na subida.");
        }
    }
}

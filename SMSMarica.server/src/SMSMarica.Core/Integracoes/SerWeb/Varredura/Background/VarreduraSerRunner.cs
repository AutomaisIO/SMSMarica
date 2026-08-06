using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Integracoes.SerWeb.Varredura.Background;

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
        await LimparOrfasAsync(stoppingToken);

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
                    pedido.UsuarioId, pedido.UsuarioNome, stoppingToken);

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
    /// Fecha rodadas que ficaram <c>EmExecucao</c> quando o processo caiu (deploy, restart,
    /// crash). Sem isso elas ficam abertas para sempre e o guard de concorrência —
    /// que olha o banco — passa a recusar toda varredura nova, com a tela dizendo
    /// "já existe uma em andamento" sobre algo que morreu há dias.
    /// </summary>
    private async Task LimparOrfasAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();

            var orfas = await db.SerVarreduraExecucoes
                .Where(x => x.Status == StatusVarreduraSer.EmExecucao
                            || x.Status == StatusVarreduraSer.Pendente)
                .ToListAsync(cancellationToken);

            if (orfas.Count == 0) return;

            foreach (var execucao in orfas)
            {
                // Erro, não Concluída: a cobertura ficou incompleta e fingir o contrário faria o
                // operador acreditar que a base está inteira.
                execucao.Status = StatusVarreduraSer.Erro;
                execucao.MensagemErro =
                    "Interrompida pelo desligamento do serviço (deploy/restart). Cobertura incompleta.";
                execucao.FinalizadoEm = DateTime.UtcNow;
                execucao.DuracaoSegundos =
                    (int)(execucao.FinalizadoEm.Value - execucao.IniciadoEm).TotalSeconds;
            }

            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("SER: {Qtd} varredura(s) órfã(s) fechada(s) na subida do serviço.", orfas.Count);
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode impedir o runner de subir.
            logger.LogError(ex, "SER: não foi possível fechar varreduras órfãs na subida.");
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
}

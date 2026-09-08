using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Varredura.Background;

/// <summary>
/// Consome a fila e roda a varredura FORA da request: uma varredura leva minutos, e fechar a aba
/// não pode matá-la no meio.
/// </summary>
public sealed class VarreduraSisregRunner(
    IVarreduraSisregFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<VarreduraSisregRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReconciliarAoSubirAsync(stoppingToken);

        await foreach (var job in fila.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Escopo próprio: o da request já morreu quando a varredura começa.
                using var scope = scopeFactory.CreateScope();
                var servico = scope.ServiceProvider.GetRequiredService<IVarreduraAgendaService>();
                await servico.ExecutarAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Sem este catch, BackgroundServiceExceptionBehavior=StopHost derruba a API inteira
                // por causa da varredura de uma unidade.
                logger.LogError(ex, "Erro inesperado na varredura {ExecucaoId} da unidade {UnidadeId}.",
                    job.ExecucaoId, job.UnidadeId);
            }
        }
    }

    /// <summary>
    /// Fecha, no start, toda execução que ficou marcada como em andamento.
    ///
    /// <para><b>Aqui "morta" é fato, não presunção.</b> O processo acabou de subir: nenhuma
    /// execução de um processo anterior pode estar viva. É o que diferencia esta faxina da que
    /// acontecia só ao criar a próxima varredura — aquela precisava adivinhar pela IDADE, e
    /// adivinhar errado, em 06/09/2026, virou um laço que matava a corrida viva e queimava 57
    /// requisições por hora.</para>
    ///
    /// <para>Sem isto, um deploy no meio de uma varredura deixava a linha dizendo "Rodando" até
    /// alguém disparar outra <b>daquela mesma unidade</b> — em 08/09/2026 foram 65 minutos de
    /// operador olhando um contador parado sem saber se era lentidão ou morte.</para>
    ///
    /// <para>⚠️ <b>Vale enquanto for uma instância só</b> (hoje, um systemd). Com réplicas, o start
    /// de uma fecharia as execuções vivas da outra — aí o critério teria de ser o batimento
    /// (<c>ultimo_sinal_em</c>) e não o start.</para>
    /// </summary>
    private async Task ReconciliarAoSubirAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();

            var fechadas = await db.SisregVarreduraExecucoes
                .Where(e => e.Status == StatusVarredura.Pendente || e.Status == StatusVarredura.EmExecucao)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Status, StatusVarredura.Erro)
                    .SetProperty(e => e.FinalizadoEm, DateTime.UtcNow)
                    .SetProperty(e => e.MensagemErro, e => e.MensagemErro ??
                        "Varredura interrompida: o serviço reiniciou (deploy ou queda) enquanto ela "
                        + "rodava. Nada do que já entrou foi perdido — basta rodar de novo."),
                    ct);

            if (fechadas > 0)
            {
                logger.LogWarning(
                    "SISREG_VARREDURA_RECONCILIADA: {Qtd} execução(ões) ficaram marcadas como em "
                    + "andamento de antes deste start e foram encerradas.", fechadas);
            }
        }
        catch (Exception ex)
        {
            // Nunca impedir o runner de subir por causa da faxina: sem ele, nenhuma varredura roda.
            logger.LogError(ex, "Falha ao reconciliar execuções de varredura no start.");
        }
    }
}

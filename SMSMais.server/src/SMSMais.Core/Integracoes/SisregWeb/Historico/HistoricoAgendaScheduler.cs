using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Integracoes.SisregWeb.Varredura;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Historico;

/// <summary>
/// Traz o PASSADO da agenda, unidade a unidade, andando para trás em fatias de 31 dias.
///
/// <para><b>Por que não é um "botão que roda de uma vez".</b> Cobrir a rede inteira desde o começo
/// custa da ordem de 1.500 requisições e o teto anti-robô é por hora — um comando único ou estouraria
/// o orçamento ou ficaria horas preso. Aqui não há execução longa: <b>cada tick avança uma fatia</b>,
/// e o estado de onde parou mora no banco (<see cref="SisregVarreduraAgenda.HistoricoCobertoDe"/>).
/// Reiniciar o servidor no meio não perde nada, e ligar de novo é barato porque o que já está
/// coberto é pulado.</para>
///
/// <para><b>Não reimplementa a importação.</b> Cada fatia é o mesmo caminho do backfill manual por
/// período, que já cria unidade, profissional e procedimento que não existiam, resolve o paciente e
/// guarda a linha crua — que é exatamente o que se quer para estudo do passado. A única coisa que o
/// período suprime é o aviso ao paciente: mandar WhatsApp sobre consulta de meses atrás seria
/// constrangedor.</para>
///
/// <para><b>Onde a unidade "começa" quem decide é o dado.</b> O motor para depois de
/// <see cref="HistoricoOpcoes.FatiasVaziasParaConcluir"/> fatias consecutivas sem nenhum registro —
/// seis meses, no padrão. Confiar na data de cadastro do SISREG seria pior: o Centro Materno
/// Infantil declara escala desde 10/09/1986, e seguir isso custaria 472 requisições procurando
/// agendamento de 40 anos atrás que nunca existiu.</para>
/// </summary>
public sealed class HistoricoAgendaScheduler(
    IServiceScopeFactory scopeFactory,
    Varredura.Background.VarreduraSisregEstadoVivo varreduraEstadoVivo,
    Importacao.Background.SisregImportacaoEstadoVivo importacaoEstadoVivo,
    MapeamentoLote.Background.MapeamentoLoteEstadoVivo loteEstadoVivo,
    Escalas.Background.EscalasSincronizacaoEstadoVivo escalasEstadoVivo,
    SisregOrcamentoRequisicoes orcamento,
    IOptions<HistoricoOpcoes> opcoes,
    IOptions<SisregOrcamentoOpcoes> orcamentoOpcoes,
    ILogger<HistoricoAgendaScheduler> logger) : BackgroundService
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly HistoricoOpcoes _opcoes = opcoes.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(15, _opcoes.TickSegundos));
        using var timer = new PeriodicTimer(intervalo);

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
                logger.LogError(ex, "Erro no tick do motor de histórico da agenda SISREG.");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // Barato: se já há trabalho vivo, nem abre escopo de DI. Todos dividem a mesma sessão.
        if (varreduraEstadoVivo.ObterAtual() is not null
            || importacaoEstadoVivo.ObterAtual() is not null
            || loteEstadoVivo.EmExecucao
            || escalasEstadoVivo.EmExecucao)
        {
            return;
        }

        // O histórico é trabalho de fundo: cede a folga do orçamento para quem está operando hoje.
        if (orcamento.Restante(orcamentoOpcoes.Value.TetoAutomatico) < _opcoes.OrcamentoMinimo) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();

        // NÃO consulta a chave-mestra de propósito. Ela pausa a AGENDA DIÁRIA das unidades — o que
        // roda sem ninguém pedir. Aqui, `HistoricoAtivo` já É o comando de uma pessoa: ela clicou
        // em "Importar o passado" e espera que ande até o fim, sozinha. Gatear isto fazia o botão
        // não produzir efeito nenhum, que foi o defeito relatado em 05/09/2026.
        //
        // O que protege a operação não é aquela chave e sim os freios acima, que são mais fortes
        // para este caso: o motor cede a vez a QUALQUER outro trabalho do SISREG e só toca com
        // folga larga de orçamento. Ele nunca disputa a sessão — no máximo espera.

        // Quem está mais atrás vai primeiro: com várias unidades ligadas, isso é rodízio justo em
        // vez de uma unidade monopolizar o motor até terminar.
        var agenda = await db.SisregVarreduraAgendas
            .Where(a => a.HistoricoAtivo && a.HistoricoConcluidoEm == null)
            .OrderByDescending(a => a.HistoricoCobertoDe == null)
            .ThenByDescending(a => a.HistoricoCobertoDe)
            .FirstOrDefaultAsync(ct);

        if (agenda is null) return;

        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Brasilia));

        // A reconciliação (ler a fatia corrente, decidir, gravar a cobertura) é COMPARTILHADA com o
        // disparo manual. Quando era só daqui, o botão manual começava a fatia e nunca movia a
        // cobertura — e como este scheduler para com o sincronismo automático desligado, cada
        // clique repetia a mesma janela. Ver AvancoHistorico.
        var reconciliacao = await AvancoHistorico.ReconciliarAsync(db, agenda, hoje, _opcoes, ct);

        if (reconciliacao.Passo == PassoHistorico.Bloquear)
        {
            logger.LogWarning(
                "SISREG_HISTORICO_BLOQUEADO: unidade {UnidadeId} — a fatia {Inicio} a {Fim} falhou "
                + "{Tentativas} vezes; o histórico foi desligado para não seguir gastando orçamento.",
                agenda.UnidadeId, reconciliacao.Inicio, reconciliacao.Fim, _opcoes.TentativasPorFatia);
            return;
        }

        if (reconciliacao.Passo == PassoHistorico.Concluir)
        {
            logger.LogInformation(
                "SISREG_HISTORICO_FIM: unidade {UnidadeId} — {Vazias} fatias seguidas sem registro.",
                agenda.UnidadeId, agenda.HistoricoFatiasVazias);
            return;
        }

        // Rodando, ou terminou mal e será repetida no próximo tick: nos dois casos a cobertura fica
        // onde está — repetir custa uma requisição, um buraco silencioso custa a análise.
        if (reconciliacao.Passo == PassoHistorico.Esperar) return;

        var servico = scope.ServiceProvider.GetRequiredService<IVarreduraAgendaService>();
        var id = await servico.IniciarPeriodoAgendadoAsync(
            agenda.UnidadeId, reconciliacao.Inicio, reconciliacao.Fim, ct);

        if (id is not null)
        {
            logger.LogInformation(
                "SISREG_HISTORICO_FATIA: unidade {UnidadeId}, {Inicio} a {Fim}.",
                agenda.UnidadeId, reconciliacao.Inicio, reconciliacao.Fim);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Historico;

/// <summary>O que sobrou para fazer depois de reconciliar a fatia corrente.</summary>
/// <param name="Passo">A decisão aplicada sobre a fatia que já existia.</param>
/// <param name="Inicio">Início da fatia a pedir agora (só vale para <c>Pedir</c>/<c>Repetir</c>).</param>
/// <param name="Fim">Fim dessa fatia.</param>
public readonly record struct ReconciliacaoHistorico(PassoHistorico Passo, DateOnly Inicio, DateOnly Fim);

/// <summary>
/// Lê o estado da fatia corrente, aplica a decisão e diz o que pedir em seguida.
///
/// <para><b>Por que isto existe como peça própria.</b> Esta rotina morava só dentro do
/// <see cref="HistoricoAgendaScheduler"/>. Quando o disparo manual passou a existir, ele começou a
/// fatia mas <b>não avançava a cobertura</b> — porque avançar era privilégio do scheduler, e o
/// scheduler para com o sincronismo automático desligado. O efeito em produção, medido em
/// 05/09/2026 no CDT: duas execuções manuais na <b>mesma</b> janela (05/08 a 04/09), a primeira
/// trazendo 3.923 registros e a segunda repetindo tudo, com <c>historico_coberto_de</c> eternamente
/// nulo. Cada clique gastava requisição para reimportar o que já estava lá.</para>
///
/// <para>Manter uma cópia da regra em cada caminho é o que produz esse tipo de divergência, então
/// os dois passaram a chamar isto.</para>
/// </summary>
public static class AvancoHistorico
{
    /// <summary>
    /// Reconcilia a fatia corrente da unidade e devolve o próximo passo.
    ///
    /// <para><b>Não</b> chama o SISREG: só lê a última execução daquela janela, decide, e grava a
    /// cobertura quando ela de fato avançou. Quem dispara a requisição é o chamador — o scheduler
    /// em silêncio, o comando manual falando alto.</para>
    ///
    /// <para>Salva as próprias alterações: avançar a cobertura e depois falhar ao pedir a fatia
    /// nova é preferível a perder o avanço de uma fatia que já foi importada.</para>
    /// </summary>
    public static async Task<ReconciliacaoHistorico> ReconciliarAsync(
        SmsMaisDbContext db,
        SisregVarreduraAgenda agenda,
        DateOnly hoje,
        HistoricoOpcoes opcoes,
        CancellationToken ct,
        bool nenhumTrabalhoVivo = false)
    {
        var (inicio, fim) = DecididorHistorico.ProximaFatia(
            agenda.HistoricoCobertoDe, hoje, opcoes.DiasPorFatia);

        // Uma execução CONCLUÍDA vence a mais recente, e a ordem importa: "esta janela está
        // coberta?" se responde por ter dado certo alguma vez, não pela última tentativa. Sem isso,
        // um restart no meio de uma re-execução faria o motor refazer uma fatia que já tinha
        // entrado — no CDT, em 06/09/2026, seriam 21 requisições e 22 minutos para rebuscar 3.923
        // registros que já estavam no banco.
        var execucao = await db.SisregVarreduraExecucoes
            .AsNoTracking()
            .Where(e => e.UnidadeId == agenda.UnidadeId && e.JanelaInicio == inicio && e.JanelaFim == fim)
            .OrderByDescending(e => e.Status == StatusVarredura.Concluida)
            .ThenByDescending(e => e.IniciadoEm)
            .FirstOrDefaultAsync(ct);

        var passo = DecididorHistorico.Decidir(
            execucao?.Status,
            execucao?.RegistrosEncontrados ?? 0,
            agenda.HistoricoFatiasVazias,
            opcoes.FatiasVaziasParaConcluir,
            nenhumTrabalhoVivo);

        // Nada a gravar: a fatia ainda não foi pedida, está rodando, ou terminou mal e será
        // repetida — em nenhum desses casos a cobertura pode andar.
        if (passo is PassoHistorico.Pedir or PassoHistorico.Esperar or PassoHistorico.Repetir)
        {
            return new ReconciliacaoHistorico(passo, inicio, fim);
        }

        agenda.HistoricoCobertoDe = inicio;
        agenda.HistoricoFatiasVazias = DecididorHistorico.ProximasVazias(
            agenda.HistoricoFatiasVazias, execucao!.RegistrosEncontrados);

        if (passo == PassoHistorico.Concluir)
        {
            agenda.HistoricoConcluidoEm = DateTime.UtcNow;
        }

        agenda.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Com a cobertura movida, a próxima fatia é outra — e ela ainda não foi pedida.
        var (proxInicio, proxFim) = DecididorHistorico.ProximaFatia(
            agenda.HistoricoCobertoDe, hoje, opcoes.DiasPorFatia);

        return new ReconciliacaoHistorico(passo, proxInicio, proxFim);
    }
}

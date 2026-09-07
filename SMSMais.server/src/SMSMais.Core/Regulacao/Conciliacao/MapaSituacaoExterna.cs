using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Regulacao.Conciliacao;

/// <summary>
/// Traduz a situação de cada sistema de regulação para o nosso estado (plano 05).
///
/// <para><b>Nem toda situação de lá tem tradução</b>, e é de propósito: <c>null</c> significa "não
/// mexe no nosso estado". Mapear tudo à força faria a varredura empurrar a solicitação para
/// frente e para trás a cada passada, com base em nuances que não mudam o que a unidade precisa
/// fazer.</para>
///
/// <para>Puro e estático — a régua é a regra de negócio destilada, e se testa sem banco.</para>
/// </summary>
public static class MapaSituacaoExterna
{
    /// <summary>
    /// <c>Pendente</c> vira <see cref="StatusRegulacao.EmFilaExterna"/>, e não um estado próprio:
    /// a pendência em si é tratada no plano 06, com o texto do follow-up. Aqui só importa que o
    /// caso continua na fila de lá.
    /// </summary>
    public static StatusRegulacao? DeSer(SituacaoSer situacao) => situacao switch
    {
        SituacaoSer.EmFila or SituacaoSer.Pendente => StatusRegulacao.EmFilaExterna,

        // Chegada confirmada é "compareceu" — o caso segue agendado até virar alta.
        SituacaoSer.Agendada or SituacaoSer.ChegadaNaoConfirmada or SituacaoSer.ChegadaConfirmada
            => StatusRegulacao.Agendada,

        SituacaoSer.Alta => StatusRegulacao.Concluida,
        SituacaoSer.Cancelada => StatusRegulacao.Cancelada,
        _ => null,
    };

    public static StatusRegulacao? DeSernit(SituacaoSernit situacao) => situacao switch
    {
        SituacaoSernit.EmFila or SituacaoSernit.Pendente => StatusRegulacao.EmFilaExterna,
        SituacaoSernit.Agendada or SituacaoSernit.ChegadaNaoConfirmada or SituacaoSernit.ChegadaConfirmada
            => StatusRegulacao.Agendada,
        SituacaoSernit.Alta => StatusRegulacao.Concluida,
        SituacaoSernit.Cancelada => StatusRegulacao.Cancelada,
        _ => null,
    };

    public static StatusRegulacao? DeSisreg(StatusSolicitacao status) => status switch
    {
        StatusSolicitacao.Solicitada => StatusRegulacao.EmFilaExterna,
        StatusSolicitacao.Agendada => StatusRegulacao.Agendada,
        StatusSolicitacao.Realizada => StatusRegulacao.Concluida,
        StatusSolicitacao.Cancelada => StatusRegulacao.Cancelada,
        _ => null,
    };

    /// <summary>
    /// A situação lida lá fora deve mudar o nosso estado?
    ///
    /// <para><b>Aqui a volta é permitida</b> — <c>Agendada → EmFilaExterna</c> acontece de
    /// verdade: o SER desmarca e o caso retorna à fila. O plano 05 pedia uma régua de "não
    /// regride" para proteger de leitura atrasada, mas ela protegeria do problema errado: a
    /// conciliação lê o <b>espelho</b>, que guarda a situação ATUAL da última varredura, e não um
    /// histórico de eventos fora de ordem. Travar a volta faria a nossa ficha dizer "agendada"
    /// para um paciente que perdeu a vaga — e ninguém descobriria pela tela.</para>
    ///
    /// <para>Quem impede o absurdo é a máquina de estados: de <c>Concluida</c>, <c>Cancelada</c> e
    /// <c>Recusada</c> não sai transição nenhuma.</para>
    /// </summary>
    public static bool DeveAplicar(StatusRegulacao atual, StatusRegulacao novo) => atual != novo;
}

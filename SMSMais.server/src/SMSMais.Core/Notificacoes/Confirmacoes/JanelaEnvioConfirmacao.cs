using SMSMais.Core.Common.Tempo;

namespace SMSMais.Core.Notificacoes.Confirmacoes;

/// <summary>
/// Janela de horário (Brasília) em que a confirmação de agendamento pode sair. Fora dela a
/// comunicação fica EMPILHADA na fila e sai quando a janela abre — ninguém recebe mensagem de
/// confirmação às 22h porque a sincronização do SISREG rodou à noite.
/// <para>Início inclusivo, fim exclusivo (08:00–18:00 ⇒ 17:59 sai, 18:00 não).</para>
/// </summary>
public static class JanelaEnvioConfirmacao
{
    public static bool Dentro(DateTime agoraUtc, TimeOnly inicio, TimeOnly fim)
    {
        if (inicio == fim) return true; // janela degenerada = sem restrição
        var hora = TimeOnly.FromDateTime(FusoBrasilia.ParaExibicao(agoraUtc));
        return inicio < fim
            ? hora >= inicio && hora < fim
            : hora >= inicio || hora < fim; // janela que atravessa a meia-noite
    }

    /// <summary>
    /// Folga da véspera: a janela já fechou, mas o atendimento é AMANHÃ e ainda não passou do
    /// <paramref name="limiteVespera"/> — a confirmação sai hoje, em vez de esperar a janela abrir e
    /// chegar no próprio dia. Só estende o fim da janela; nunca antecipa o início.
    /// </summary>
    public static bool NaFolgaDaVespera(DateTime agoraUtc, DateTime dataAgendadaUtc, TimeOnly fim, TimeOnly limiteVespera)
    {
        if (limiteVespera <= fim) return false; // a janela já vai até lá (ou além)
        var agora = FusoBrasilia.ParaExibicao(agoraUtc);
        var hora = TimeOnly.FromDateTime(agora);
        return hora >= fim && hora < limiteVespera
            && FusoBrasilia.ParaExibicao(dataAgendadaUtc).Date == agora.Date.AddDays(1);
    }

    /// <summary>O atendimento é hoje (data de Brasília)?</summary>
    public static bool AtendimentoHoje(DateTime agoraUtc, DateTime dataAgendadaUtc) =>
        FusoBrasilia.ParaExibicao(dataAgendadaUtc).Date == FusoBrasilia.ParaExibicao(agoraUtc).Date;

    /// <summary>Próximo instante (UTC) em que a janela abre; <paramref name="agoraUtc"/> se já está aberta.</summary>
    public static DateTime ProximaAbertura(DateTime agoraUtc, TimeOnly inicio, TimeOnly fim)
    {
        if (Dentro(agoraUtc, inicio, fim)) return agoraUtc;
        var local = FusoBrasilia.ParaExibicao(agoraUtc);
        var abertura = local.Date.Add(inicio.ToTimeSpan());
        if (abertura <= local) abertura = abertura.AddDays(1);
        return FusoBrasilia.DeBrasiliaParaUtc(abertura);
    }
}

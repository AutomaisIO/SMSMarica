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

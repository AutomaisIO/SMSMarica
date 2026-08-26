namespace SMSMais.Core.RoboAtendimento.Runtime;

/// <summary>
/// Regras da trava "humano-por-janela" partilhadas pelo handler (enfileira) e pelo processador
/// (responde), para não divergirem: a âncora efetiva da janela e o override por horário do
/// expediente humano (fora dele o robô assume mesmo com humano na sessão).
/// </summary>
public static class TravaHumano
{
    /// <summary>Âncora a partir da qual a atividade humana conta. É o início da janela corrente,
    /// mas nunca antes de um re-arme (encaminhar ao robô move a âncora para frente, ignorando o
    /// que o humano fez antes).</summary>
    public static DateTime AncoraEfetiva(DateTime? janelaAbertaEm, DateTime fallback, DateTime? roboRearmadoEm)
    {
        var baseAncora = janelaAbertaEm ?? fallback;
        return roboRearmadoEm is { } r && r > baseAncora ? r : baseAncora;
    }

    /// <summary>Verdadeiro quando o horário atual (Brasília, UTC-3) está FORA do expediente dos
    /// atendentes humanos — antes de <paramref name="inicio"/> ou a partir de <paramref name="fim"/>.
    /// Nesse caso o robô ignora a trava humano-por-janela e assume a conversa. Ambos nulos ⇒ nunca
    /// assume por horário (retorna falso).</summary>
    public static bool ForaDoExpedienteHumano(TimeOnly? inicio, TimeOnly? fim, DateTime agoraUtc)
    {
        if (inicio is null && fim is null) return false;
        var hora = TimeOnly.FromDateTime(agoraUtc.AddHours(-3)); // regra única de fuso: Brasília fixo
        if (inicio is { } ini && hora < ini) return true;        // atendentes ainda não chegaram
        if (fim is { } f && hora >= f) return true;              // atendentes já saíram
        return false;
    }
}

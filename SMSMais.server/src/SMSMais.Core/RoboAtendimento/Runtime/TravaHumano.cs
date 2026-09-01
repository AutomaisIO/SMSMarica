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

    /// <summary>Fora do expediente, por quanto tempo o robô ainda RECUA depois da última ação de um
    /// humano. Enquanto um atendente estiver ativo (agiu há menos que isto), o robô não assume,
    /// mesmo após o fim do expediente — cobre o atendente que ficou trabalhando além da hora.</summary>
    public static readonly TimeSpan RecenciaForaExpediente = TimeSpan.FromMinutes(120);

    /// <summary>Instante-corte a partir do qual a atividade humana cala o robô. Dentro do expediente
    /// é a âncora da janela; FORA dele, só a atividade RECENTE cala (max entre a âncora e agora−recência),
    /// de modo que o robô assume quando os atendentes já saíram, mas recua se um deles acabou de agir.</summary>
    public static DateTime CorteHumano(DateTime ancora, bool foraExpediente, DateTime agoraUtc)
    {
        if (!foraExpediente) return ancora;
        var recente = agoraUtc - RecenciaForaExpediente;
        return recente > ancora ? recente : ancora;
    }

    /// <summary>Verdadeiro quando o horário atual (Brasília, UTC-3) está FORA do expediente dos
    /// atendentes humanos — dia da semana fora do expediente, antes de <paramref name="inicio"/> ou
    /// a partir de <paramref name="fim"/>. Nesse caso o robô ignora a trava humano-por-janela e
    /// assume a conversa. Tudo nulo ⇒ nunca assume por horário (retorna falso).
    ///
    /// <para>O parâmetro <paramref name="diasSemana"/> (bitmask, bit 0 = domingo, como em
    /// <c>RoboAssunto.DiasSemana</c>) existe porque a regra só de hora tratava SÁBADO 11h como
    /// "atendente disponível": numa simulação sobre caso real, o robô prometeu "vou conectar você
    /// agora" num sábado — e o cidadão esperaria em silêncio até segunda.</para></summary>
    public static bool ForaDoExpedienteHumano(TimeOnly? inicio, TimeOnly? fim, DateTime agoraUtc, int? diasSemana = null)
    {
        var agora = agoraUtc.AddHours(-3); // regra única de fuso: Brasília fixo
        if (diasSemana is int mask && (mask & (1 << (int)agora.DayOfWeek)) == 0)
            return true;                                         // dia sem expediente humano
        if (inicio is null && fim is null) return false;
        var hora = TimeOnly.FromDateTime(agora);
        if (inicio is { } ini && hora < ini) return true;        // atendentes ainda não chegaram
        if (fim is { } f && hora >= f) return true;              // atendentes já saíram
        return false;
    }
}

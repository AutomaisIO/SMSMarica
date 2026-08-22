using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Core.Integracoes.SisregWeb.Varredura.Background;

/// <summary>Por que o scheduler disparou (ou não) a varredura de uma unidade.</summary>
public enum DecisaoVarredura
{
    Disparar,
    Aguardar,

    /// <summary>Fora da janela permitida — varrer no expediente derrubaria o atendente da unidade.</summary>
    ForaDaJanela,

    /// <summary>Já há varredura ou importação viva. O humano sempre ganha do robô.</summary>
    PularRunVivo,
}

/// <summary>
/// Decide se a varredura de uma unidade deve disparar. <b>Puro</b>: sem relógio, sem banco, sem
/// I/O — o "agora" entra por parâmetro. É o que torna o comportamento do motor testável sem subir
/// nada.
/// </summary>
public static class DecididorVarreduraSisreg
{
    /// <summary>Teto do backoff: mesmo com falha repetida, tenta pelo menos uma vez por dia.</summary>
    private static readonly TimeSpan TetoBackoff = TimeSpan.FromHours(8);

    public static DecisaoVarredura Decidir(
        SisregVarreduraAgenda agenda,
        DateTime agoraUtc,
        TimeOnly horaLocalAgora,
        TimeOnly corteEntrada,
        TimeOnly bloqueioFim,
        bool varreduraViva,
        bool importacaoViva)
    {
        if (!agenda.Ativo) return DecisaoVarredura.Aguardar;
        if (agenda.PausadoAte is { } pausa && pausa > agoraUtc) return DecisaoVarredura.Aguardar;

        // NULL = NÃO elegível. É o inverso da agenda do PEP, e de propósito: numa agenda "diária às
        // HH:mm", tratar null como "elegível já" faria o primeiro tick depois de um deploy disparar
        // a varredura no meio da tarde — derrubando a sessão de quem está atendendo. Quem liga a
        // agenda calcula o ProximoRunEm ao salvar.
        if (agenda.ProximoRunEm is not { } proximo || proximo > agoraUtc) return DecisaoVarredura.Aguardar;

        if (!PodeIniciar(horaLocalAgora, corteEntrada, bloqueioFim)) return DecisaoVarredura.ForaDaJanela;

        // O humano sempre ganha: uma importação manual em curso usa a mesma saída para o SISREG.
        if (varreduraViva || importacaoViva) return DecisaoVarredura.PularRunVivo;

        return DecisaoVarredura.Disparar;
    }

    /// <summary>
    /// Pode INICIAR uma varredura nesta hora local? O <c>expo_solicitacoes</c> fica bloqueado pelo
    /// SISREG de <paramref name="bloqueioFim"/> para trás; <paramref name="corteEntrada"/> antecipa
    /// o corte com uma margem, para não começar uma varredura que cruzaria o bloqueio. Recusa a
    /// faixa <c>(corteEntrada, bloqueioFim]</c> — fora dela, roda a qualquer hora.
    /// </summary>
    public static bool PodeIniciar(TimeOnly hora, TimeOnly corteEntrada, TimeOnly bloqueioFim) =>
        !(hora > corteEntrada && hora <= bloqueioFim);

    /// <summary>Próximo <c>HH:mm</c> local estritamente depois de agora, devolvido em UTC.</summary>
    public static DateTime ProximoDiario(TimeOnly horaLocal, DateTime agoraUtc, TimeZoneInfo fuso)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, fuso);
        var alvo = local.Date + horaLocal.ToTimeSpan();
        if (alvo <= local) alvo = alvo.AddDays(1);

        return ParaUtc(alvo, fuso);
    }

    /// <summary>
    /// Depois de um erro: backoff exponencial a partir de 15 min, <b>limitado ao próximo horário
    /// diário</b>. Sem esse limite, uma sequência de falhas empurraria a unidade para além do dia
    /// seguinte e ela simplesmente pararia de ser varrida sem ninguém notar.
    /// </summary>
    public static DateTime ProximoAposErro(SisregVarreduraAgenda agenda, DateTime agoraUtc, TimeZoneInfo fuso)
    {
        var expoente = Math.Clamp(agenda.FalhasConsecutivas, 0, 5);
        var atraso = TimeSpan.FromMinutes(15 * Math.Pow(2, expoente));
        if (atraso > TetoBackoff) atraso = TetoBackoff;

        var cedo = agoraUtc + atraso;
        var diario = ProximoDiario(agenda.HoraLocal, agoraUtc, fuso);

        return cedo > diario ? diario : cedo;
    }

    /// <summary>
    /// O cursor de retomada só vale enquanto a janela de datas for a mesma. Janela vencida é
    /// passado, e refazer o passado gasta orçamento com dado que não muda mais.
    /// </summary>
    public static bool CursorValido(SisregVarreduraAgenda agenda, DateOnly janelaFimAtual) =>
        agenda.CursorProfissionalCpf is not null
        && agenda.CursorJanelaFim is { } fim
        && fim >= janelaFimAtual;

    private static DateTime ParaUtc(DateTime local, TimeZoneInfo fuso)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), fuso);
        }
        catch (ArgumentException)
        {
            // Horário inexistente numa virada de DST. Brasília não tem desde 2019, mas custa uma
            // linha não depender disso.
            return TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(local.AddHours(1), DateTimeKind.Unspecified), fuso);
        }
    }
}

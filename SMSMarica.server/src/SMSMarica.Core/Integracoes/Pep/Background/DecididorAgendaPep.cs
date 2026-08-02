using SMSMarica.Data.Entities.Pep;

namespace SMSMarica.Core.Integracoes.Pep.Background;

/// <summary>Resultado da avaliação de uma agenda num tick do scheduler.</summary>
public enum DecisaoAgenda
{
    /// <summary>Elegível — enfileirar uma execução incremental agora.</summary>
    Disparar,

    /// <summary>Ainda não chegou a hora (<c>ProximoRunEm</c> no futuro ou pausa administrativa).</summary>
    Aguardar,

    /// <summary>Há um run vivo (manual ou agendado) — o agendado nunca compete, só pula.</summary>
    PularRunVivo,

    /// <summary>Fora da janela de execução configurada.</summary>
    ForaDaJanela,
}

/// <summary>
/// Lógica PURA de decisão do sincronismo contínuo (ADR-0024) — sem relógio, sem I/O, sem
/// timer: recebe o estado e o "agora" e devolve a decisão. Testável de ponta a ponta.
/// </summary>
public static class DecididorAgendaPep
{
    /// <summary>Teto do backoff exponencial após falhas consecutivas.</summary>
    public static readonly TimeSpan TetoBackoff = TimeSpan.FromHours(4);

    public static DecisaoAgenda Decidir(PepSincronizacaoAgenda agenda, DateTime agoraUtc, bool runVivo, TimeOnly horaLocalBrasilia)
    {
        if (!agenda.Ativo) return DecisaoAgenda.Aguardar;
        if (agenda.PausadoAte is { } pausa && pausa > agoraUtc) return DecisaoAgenda.Aguardar;
        if (agenda.ProximoRunEm is { } proximo && proximo > agoraUtc) return DecisaoAgenda.Aguardar;
        if (!DentroDaJanela(agenda.JanelaInicioLocal, agenda.JanelaFimLocal, horaLocalBrasilia)) return DecisaoAgenda.ForaDaJanela;
        if (runVivo) return DecisaoAgenda.PularRunVivo;
        return DecisaoAgenda.Disparar;
    }

    /// <summary>Janela em hora local; início &gt; fim = janela que cruza a meia-noite (ex.: 22:00–05:00).</summary>
    public static bool DentroDaJanela(TimeOnly? inicio, TimeOnly? fim, TimeOnly agora)
    {
        if (inicio is not { } i || fim is not { } f || i == f) return true;
        return i < f ? agora >= i && agora < f : agora >= i || agora < f;
    }

    /// <summary>Próximo disparo após sucesso (ou cancelamento): o intervalo normal.</summary>
    public static DateTime ProximoAposSucesso(PepSincronizacaoAgenda agenda, DateTime agoraUtc) =>
        agoraUtc.AddMinutes(Math.Max(1, agenda.IntervaloMinutos));

    /// <summary>
    /// Próximo disparo após erro: backoff exponencial (intervalo × 2^falhas) com teto —
    /// evita martelar um Oracle/hub doente e dá tempo de alguém ver o alerta.
    /// </summary>
    public static DateTime ProximoAposErro(PepSincronizacaoAgenda agenda, DateTime agoraUtc)
    {
        var expoente = Math.Clamp(agenda.FalhasConsecutivas, 0, 10);
        var atraso = TimeSpan.FromMinutes(Math.Max(1, agenda.IntervaloMinutos) * Math.Pow(2, expoente));
        return agoraUtc + (atraso > TetoBackoff ? TetoBackoff : atraso);
    }

    /// <summary>Re-scan integral de médicos quando a marca envelhece além do configurado.</summary>
    public static bool DeveForcarMedicos(DateTime? medicoEm, int rescanHoras, DateTime agoraUtc) =>
        medicoEm is null || agoraUtc - medicoEm.Value >= TimeSpan.FromHours(Math.Max(1, rescanHoras));
}

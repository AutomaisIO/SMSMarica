namespace SMSMais.Core.Common.Tempo;

/// <summary>
/// Janela de horário do dia (Brasília), no formato que as configurações usam: início e fim.
/// Janela que vira o dia (22:00–06:00) vale; início = fim é o dia inteiro. O fim não está dentro.
/// </summary>
public static class JanelaDoDia
{
    public static bool Contem(TimeOnly agora, TimeOnly inicio, TimeOnly fim) =>
        inicio == fim || (inicio < fim ? agora >= inicio && agora < fim : agora >= inicio || agora < fim);

    /// <summary>A hora de agora em Brasília.</summary>
    public static TimeOnly AgoraEmBrasilia() => TimeOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
}

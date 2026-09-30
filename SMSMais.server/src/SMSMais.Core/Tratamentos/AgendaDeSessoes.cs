namespace SMSMais.Core.Tratamentos;

/// <summary>
/// Agenda do atendimento: dias da semana + N sessões, ou contínuo. Única fonte das datas — a prévia
/// do painel pede ao servidor, não recalcula.
///
/// <para>Máscara de dias: bit 0 = domingo, 1 = segunda … 6 = sábado (a convenção do
/// <see cref="DayOfWeek"/>). Contínuo nunca gera "para sempre": vai até o fim do mês seguinte, e a
/// renovação estende o horizonte de mês em mês.</para>
/// </summary>
public static class AgendaDeSessoes
{
    public const int LimiteDeSessoes = 365;

    public static bool MascaraValida(int mascara) => mascara is >= 1 and <= 127;

    public static bool TemSessaoNoDia(int mascara, DayOfWeek dia) => (mascara & (1 << (int)dia)) != 0;

    /// <summary>Último dia do mês seguinte ao de <paramref name="referencia"/> — o horizonte do contínuo.</summary>
    public static DateOnly FimDoMesSeguinte(DateOnly referencia) =>
        new DateOnly(referencia.Year, referencia.Month, 1).AddMonths(2).AddDays(-1);

    /// <summary>Horizonte de geração do contínuo: fim do mês seguinte ao de hoje — ou ao do início,
    /// quando o atendimento começa mais adiante (senão nasceria sem nenhuma sessão).</summary>
    public static DateOnly HorizonteContinuo(DateOnly dataInicio, DateOnly hoje) =>
        FimDoMesSeguinte(dataInicio > hoje ? dataInicio : hoje);

    /// <summary>As <paramref name="quantidade"/> primeiras datas a partir de <paramref name="inicio"/>
    /// (inclusive) que caem nos dias marcados. Vazio para máscara ou quantidade fora da faixa.</summary>
    public static IReadOnlyList<DateOnly> GerarQuantidade(DateOnly inicio, int mascara, int quantidade)
    {
        if (!MascaraValida(mascara) || quantidade < 1 || quantidade > LimiteDeSessoes) return [];

        var datas = new List<DateOnly>(quantidade);
        for (var data = inicio; datas.Count < quantidade; data = data.AddDays(1))
        {
            if (TemSessaoNoDia(mascara, data.DayOfWeek)) datas.Add(data);
        }
        return datas;
    }

    /// <summary>Todas as datas dos dias marcados entre <paramref name="inicio"/> e
    /// <paramref name="ate"/> (ambos inclusive).</summary>
    public static IReadOnlyList<DateOnly> GerarAte(DateOnly inicio, int mascara, DateOnly ate)
    {
        if (!MascaraValida(mascara) || ate < inicio) return [];

        var datas = new List<DateOnly>();
        for (var data = inicio; data <= ate; data = data.AddDays(1))
        {
            if (TemSessaoNoDia(mascara, data.DayOfWeek)) datas.Add(data);
        }
        return datas;
    }
}

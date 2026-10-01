namespace SMSMais.Core.Integracoes.SisregWeb.Indicadores;

/// <summary>Uma janela de coleta (a chave de <c>sisreg_indicador_coleta</c> é coletor + início + escopo).</summary>
public sealed record JanelaColeta(DateOnly Inicio, DateOnly Fim);

/// <summary>
/// Quais janelas o coletor deve ter, a partir de hoje. Puro, para teste.
///
/// <para><b>Faltas por semana</b>, nos mesmos cortes da carga do laboratório (1–8, 9–16, 17–23,
/// 24–fim): assim a primeira janela do coletor encosta na última carregada, sem sobreposição.</para>
///
/// <para><b>Escopo "dia"</b>: janela de faltas dividida depois de um 504 e o fechamento diário de
/// canceladas da conciliação. O escopo existe porque o dia 1º de um mês tem o MESMO início da
/// semana/do mês — sem ele a chave única colidiria.</para>
/// </summary>
public static class PlanoColetaIndicadores
{
    public const string EscopoDia = "dia";

    /// <summary>Escopo da amostra de motivos de um mês de canceladas (não é total: não entra no número oficial).</summary>
    public const string EscopoAmostra = "amostra";

    /// <summary>Semanas de faltas já velhas o bastante (<paramref name="diasParaFaltas"/>) dentro dos últimos meses.</summary>
    public static List<JanelaColeta> SemanasDeFaltas(DateOnly hoje, int mesesRecentes, int diasParaFaltas)
    {
        var limite = hoje.AddDays(-diasParaFaltas);
        var fora = new List<JanelaColeta>();
        foreach (var mes in MesesAte(hoje, mesesRecentes + 1, incluirMesCorrente: true))
        {
            var ultimo = mes.AddMonths(1).AddDays(-1).Day;
            foreach (var (i, f) in new[] { (1, 8), (9, 16), (17, 23), (24, ultimo) })
            {
                var j = new JanelaColeta(new DateOnly(mes.Year, mes.Month, i), new DateOnly(mes.Year, mes.Month, f));
                if (j.Fim <= limite) fora.Add(j);
            }
        }
        return fora;
    }

    /// <summary>
    /// Semanas de faltas ainda NOVAS demais para o número oficial: do corte que contém
    /// <c>hoje − diasParaFaltas</c> até ontem (a semana corrente vem cortada em ontem — o SISREG não
    /// aceita data futura, e o dia de hoje ainda está acontecendo). São o complemento exato de
    /// <see cref="SemanasDeFaltas"/>: a semana sai daqui no dia em que entra lá.
    /// </summary>
    public static List<JanelaColeta> SemanasRecentesDeFaltas(DateOnly hoje, int diasParaFaltas)
    {
        var limite = hoje.AddDays(-diasParaFaltas);
        var ontem = hoje.AddDays(-1);
        var fora = new List<JanelaColeta>();
        var primeiroMes = new DateOnly(limite.Year, limite.Month, 1);
        for (var mes = primeiroMes; mes <= ontem; mes = mes.AddMonths(1))
        {
            var ultimo = mes.AddMonths(1).AddDays(-1).Day;
            foreach (var (i, f) in new[] { (1, 8), (9, 16), (17, 23), (24, ultimo) })
            {
                var inicio = new DateOnly(mes.Year, mes.Month, i);
                var fim = new DateOnly(mes.Year, mes.Month, f);
                if (fim <= limite || inicio > ontem) continue;
                fora.Add(new JanelaColeta(inicio, fim > ontem ? ontem : fim));
            }
        }
        return fora;
    }

    /// <summary>Os <paramref name="quantos"/> meses fechados anteriores a hoje (primeiro dia de cada), do mais antigo ao mais novo.</summary>
    public static List<DateOnly> MesesFechados(DateOnly hoje, int quantos) =>
        MesesAte(hoje, quantos, incluirMesCorrente: false);

    /// <summary>Competências de PPI que já podem ser lidas: meses fechados, a partir do dia <paramref name="diaDaPpi"/> do mês seguinte.</summary>
    public static List<DateOnly> CompetenciasDePpi(DateOnly hoje, int mesesRecentes, int diaDaPpi) =>
        MesesFechados(hoje, mesesRecentes)
            .Where(m => hoje >= m.AddMonths(1).AddDays(Math.Max(1, diaDaPpi) - 1))
            .ToList();

    /// <summary>
    /// Páginas da AMOSTRA de motivos de um mês: <paramref name="quantas"/> páginas espalhadas do início ao
    /// fim da listagem (a primeira sempre entra — é ela que declara o total). Mesmo desenho da amostra do
    /// laboratório de 30/09/2026 (<c>coletar_serie_indicadores.py</c>, etapa <c>amostra</c>).
    /// </summary>
    public static List<int> PaginasDaAmostra(int paginas, int quantas)
    {
        if (paginas <= 1 || quantas <= 1) return [0];
        return Enumerable.Range(0, quantas)
            .Select(i => (int)Math.Round(i * (paginas - 1) / (double)(quantas - 1), MidpointRounding.AwayFromZero))
            .Distinct()
            .Order()
            .ToList();
    }

    /// <summary>Divide uma janela de faltas em dias (depois de o SISREG estourar o tempo na semana inteira).</summary>
    public static List<JanelaColeta> Dias(JanelaColeta janela)
    {
        var fora = new List<JanelaColeta>();
        for (var d = janela.Inicio; d <= janela.Fim; d = d.AddDays(1)) fora.Add(new JanelaColeta(d, d));
        return fora;
    }

    private static List<DateOnly> MesesAte(DateOnly hoje, int quantos, bool incluirMesCorrente)
    {
        var atual = new DateOnly(hoje.Year, hoje.Month, 1);
        var fora = new List<DateOnly>();
        for (var i = quantos - (incluirMesCorrente ? 1 : 0); i >= (incluirMesCorrente ? 0 : 1); i--)
            fora.Add(atual.AddMonths(-i));
        return fora;
    }
}

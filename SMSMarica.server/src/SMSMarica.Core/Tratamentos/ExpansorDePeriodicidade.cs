using SMSMarica.Core.Tratamentos.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Tratamentos;

/// <summary>
/// Expande uma regra de periodicidade em uma lista de datas. Fonte da
/// verdade — o frontend pode replicar para preview, mas o servidor
/// valida/recalcula antes de persistir.
/// </summary>
public static class ExpansorDePeriodicidade
{
    public const int LimiteDeSessoes = 365;

    public static IReadOnlyList<DateOnly> Expandir(ExpandirPeriodicidadeRequest r)
    {
        if (r.QuantidadeSessoes <= 0 || r.QuantidadeSessoes > LimiteDeSessoes)
        {
            return [];
        }

        return r.Tipo switch
        {
            TipoPeriodicidade.Diaria => ExpandirDiaria(r.DataInicio, r.QuantidadeSessoes),
            TipoPeriodicidade.IntervaloDias => ExpandirIntervalo(r.DataInicio, r.IntervaloDias ?? 0, r.QuantidadeSessoes),
            TipoPeriodicidade.SemanaDiasFixos => ExpandirSemanal(r.DataInicio, r.DiasSemanaMascara ?? 0, r.QuantidadeSessoes),
            TipoPeriodicidade.Manual => [],
            _ => [],
        };
    }

    private static IReadOnlyList<DateOnly> ExpandirDiaria(DateOnly inicio, int qtd)
    {
        var datas = new List<DateOnly>(qtd);
        for (var i = 0; i < qtd; i++)
        {
            datas.Add(inicio.AddDays(i));
        }
        return datas;
    }

    private static IReadOnlyList<DateOnly> ExpandirIntervalo(DateOnly inicio, int intervaloDias, int qtd)
    {
        if (intervaloDias < 1) return [];
        var datas = new List<DateOnly>(qtd);
        for (var i = 0; i < qtd; i++)
        {
            datas.Add(inicio.AddDays(i * intervaloDias));
        }
        return datas;
    }

    /// <summary>
    /// Bitmask de dias da semana: bit 0 = domingo, 1 = segunda, … 6 = sábado.
    /// (mesma convenção de DayOfWeek do .NET). Itera dia a dia a partir da
    /// data de início pegando os dias selecionados até completar <paramref name="qtd"/>.
    /// </summary>
    private static IReadOnlyList<DateOnly> ExpandirSemanal(DateOnly inicio, int mascara, int qtd)
    {
        if (mascara < 1 || mascara > 127) return [];

        var datas = new List<DateOnly>(qtd);
        var data = inicio;
        // Guarda contra loop infinito (nunca deve ultrapassar 7*qtd dias).
        var limiteIteracoes = qtd * 7 + 7;
        for (var i = 0; datas.Count < qtd && i < limiteIteracoes; i++)
        {
            var bit = 1 << (int)data.DayOfWeek;
            if ((mascara & bit) != 0)
            {
                datas.Add(data);
            }
            data = data.AddDays(1);
        }
        return datas;
    }
}

using System.Globalization;
using Npgsql;
using SMSMais.Core.Regulacao.Indicadores.Dtos;

namespace SMSMais.Core.Regulacao.Indicadores;

/// <summary>Meses do recorte (fechados, em horário de Brasília). <c>Meses</c> = "AAAA-MM".</summary>
public sealed record PeriodoIndicadores(DateOnly Inicio, DateOnly Fim, IReadOnlyList<string> Meses)
{
    public const int MaximoMeses = 24;

    public static PeriodoIndicadores De(DateOnly primeiroMes, DateOnly ultimoMes)
    {
        var ini = new DateOnly(primeiroMes.Year, primeiroMes.Month, 1);
        var ultimo = new DateOnly(ultimoMes.Year, ultimoMes.Month, 1);
        var meses = new List<string>();
        for (var m = ini; m <= ultimo; m = m.AddMonths(1)) meses.Add(Chave(m));
        return new PeriodoIndicadores(ini, ultimo.AddMonths(1).AddDays(-1), meses);
    }

    public static string Chave(DateOnly d) => d.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public IEnumerable<string> Anos => Meses.Select(m => m[..4]).Distinct();

    public DateOnly FimDoMes(string mes)
    {
        var d = DateOnly.ParseExact(mes + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return d.AddMonths(1).AddDays(-1);
    }
}

/// <summary>Monta as séries no formato do DTO — mesma semântica do relatório em PDF de 30/09/2026.</summary>
internal sealed class Series(PeriodoIndicadores periodo)
{
    public IReadOnlyList<string> Meses => periodo.Meses;

    public SerieIndicadorDto Serie(
        string rotulo, IReadOnlyDictionary<string, decimal?> valores, SeloIndicador selo, string? nota = null,
        FormatoIndicador formato = FormatoIndicador.Inteiro, bool destaque = false, bool subitem = false,
        IReadOnlyDictionary<string, decimal?>? anual = null, AgregacaoIndicador? agregacao = null)
    {
        var completo = periodo.Meses.ToDictionary(m => m, m => valores.TryGetValue(m, out var v) ? v : null);
        var agg = agregacao ?? (formato == FormatoIndicador.Inteiro ? AgregacaoIndicador.Soma : AgregacaoIndicador.Media);
        return new SerieIndicadorDto(rotulo, selo, nota, formato, destaque, subitem, completo,
            anual ?? new Dictionary<string, decimal?>(), agg);
    }

    public SerieIndicadorDto Serie(
        string rotulo, IReadOnlyDictionary<string, int> valores, SeloIndicador selo, string? nota = null,
        bool destaque = false, bool subitem = false, AgregacaoIndicador? agregacao = null) =>
        Serie(rotulo, valores.ToDictionary(kv => kv.Key, kv => (decimal?)kv.Value), selo, nota,
            FormatoIndicador.Inteiro, destaque, subitem, null, agregacao);

    /// <summary>% mês a mês = num ÷ den.</summary>
    public Dictionary<string, decimal?> Pct(IReadOnlyDictionary<string, int> num, IReadOnlyDictionary<string, int> den) =>
        periodo.Meses.ToDictionary(m => m, m =>
            den.TryGetValue(m, out var d) && d > 0 && num.TryGetValue(m, out var n)
                ? Math.Round(100m * n / d, 1) : (decimal?)null);

    /// <summary>% do ano = soma do num ÷ soma do den, só nos meses em que os dois existem.</summary>
    public Dictionary<string, decimal?> PctAnual(IReadOnlyDictionary<string, int> num, IReadOnlyDictionary<string, int> den)
    {
        var r = new Dictionary<string, decimal?>();
        foreach (var ano in periodo.Anos)
        {
            var ms = periodo.Meses.Where(m => m.StartsWith(ano, StringComparison.Ordinal) && num.ContainsKey(m)
                                              && den.TryGetValue(m, out var dm) && dm > 0).ToList();
            var somaNum = ms.Sum(m => num[m]);
            var somaDen = ms.Sum(m => den[m]);
            r[ano] = somaDen > 0 ? Math.Round(100m * somaNum / somaDen, 1) : null;
        }
        return r;
    }
}

/// <summary>SQL cru sobre a conexão do contexto, numa transação (tabelas temporárias morrem nela).</summary>
internal sealed class Sql(NpgsqlConnection conn, NpgsqlTransaction tx)
{
    public async Task<List<object?[]>> LinhasAsync(string sql, CancellationToken ct, params object?[] parametros)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx) { CommandTimeout = 180 };
        for (var i = 0; i < parametros.Length; i++)
            cmd.Parameters.Add(new NpgsqlParameter($"p{i}", parametros[i] ?? DBNull.Value));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var linhas = new List<object?[]>();
        while (await r.ReadAsync(ct))
        {
            var valores = new object?[r.FieldCount];
            for (var i = 0; i < r.FieldCount; i++) valores[i] = r.IsDBNull(i) ? null : r.GetValue(i);
            linhas.Add(valores);
        }
        return linhas;
    }

    public async Task ExecutarAsync(string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx) { CommandTimeout = 180 };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>"mês → contagem" de uma consulta que devolve (texto AAAA-MM, número).</summary>
    public async Task<Dictionary<string, int>> PorMesAsync(string sql, CancellationToken ct, params object?[] parametros)
    {
        var d = new Dictionary<string, int>();
        foreach (var l in await LinhasAsync(sql, ct, parametros))
        {
            if (l[0] is string mes && l[1] is not null) d[mes] = Convert.ToInt32(l[1], CultureInfo.InvariantCulture);
        }
        return d;
    }

    /// <summary>"categoria → mês → contagem" de (mês, categoria, número).</summary>
    public async Task<Dictionary<string, Dictionary<string, int>>> PorMesECategoriaAsync(
        string sql, CancellationToken ct, params object?[] parametros)
    {
        var d = new Dictionary<string, Dictionary<string, int>>();
        foreach (var l in await LinhasAsync(sql, ct, parametros))
        {
            if (l[0] is not string mes || l[1] is not string cat || l[2] is null) continue;
            if (!d.TryGetValue(cat, out var porMes)) d[cat] = porMes = [];
            porMes[mes] = Convert.ToInt32(l[2], CultureInfo.InvariantCulture);
        }
        return d;
    }
}

internal static class Formatar
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string? Numero(object? v) => v switch
    {
        null => null,
        int i => i.ToString("N0", PtBr),
        long l => l.ToString("N0", PtBr),
        decimal m => m.ToString("N0", PtBr),
        double d => d.ToString("N0", PtBr),
        _ => Convert.ToString(v, PtBr),
    };

    public static string? Data(object? v) => v switch
    {
        DateOnly d => d.ToString("dd/MM/yyyy", PtBr),
        DateTime dt => dt.ToString("dd/MM/yyyy", PtBr),
        _ => null,
    };

    public static decimal? Dec(object? v) => v is null ? null : Convert.ToDecimal(v, CultureInfo.InvariantCulture);
}

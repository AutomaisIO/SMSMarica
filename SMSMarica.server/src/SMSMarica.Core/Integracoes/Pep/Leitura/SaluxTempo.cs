using System.Globalization;

namespace SMSMarica.Core.Integracoes.Pep.Leitura;

/// <summary>
/// Conversões de data/hora entre o hub (UTC) e o Oracle do Salux (relógio em horário de
/// Brasília, verificado em produção com skew ≈ 0). Concentra a regra num lugar só: o
/// literal SQL vai SEMPRE em BRT e o parse de volta assume SEMPRE −03:00. Antes disto o
/// filtro usava <c>ToLocalTime()</c> — no droplet (TZ=UTC) era no-op e cada rodada
/// incremental perdia uma janela de ~3 h (defeito D5 do ADR-0024).
/// </summary>
public static class SaluxTempo
{
    // Brasil não tem horário de verão desde 2019; ainda assim usa o fuso IANA (e não um
    // offset fixo) para o literal, caso a política mude. O parse usa -03:00 fixo por ser
    // o formato que o próprio Oracle devolve via TO_CHAR.
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>Literal <c>TO_DATE</c> em BRT a partir de um instante UTC (para filtros <c>&gt; :since</c>).</summary>
    public static string LiteralOracle(DateTime utc)
    {
        var brt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Brasilia);
        return $"TO_DATE('{brt:yyyy-MM-dd HH:mm:ss}','YYYY-MM-DD HH24:MI:SS')";
    }

    /// <summary>Anexa o offset de Brasília a um <c>TO_CHAR(...,'YYYY-MM-DD"T"HH24:MI:SS')</c> vindo do Oracle.</summary>
    public static string? DtIso(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim() + "-03:00";

    /// <summary>Converte a data textual do Oracle (BRT) para <see cref="DateTime"/> UTC.</summary>
    public static DateTime? ParseUtc(string? dataOracle)
    {
        var iso = DtIso(dataOracle);
        return iso is not null && DateTime.TryParse(iso, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;
    }
}

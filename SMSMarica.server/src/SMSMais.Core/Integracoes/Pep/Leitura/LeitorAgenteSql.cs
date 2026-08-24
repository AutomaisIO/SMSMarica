using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Inteligencia.Fontes;

namespace SMSMais.Core.Integracoes.Pep.Leitura;

/// <summary>
/// Leitura tabular sobre uma base alcançada por <b>agente</b> (ADR-0023): o SQL sai daqui, o
/// agente executa no servidor de destino e devolve as linhas.
///
/// <para>Difere do <see cref="LeitorOracleHis"/> em uma coisa que muda o desenho do conector:
/// <b>não há cursor</b>. Cada chamada é uma requisição inteira, com um teto de linhas — não dá
/// para "abrir e ir lendo". Toda leitura em volume tem de ser paginada por keyset, e o conector
/// precisa saber quando uma página <i>encostou no teto</i>, porque aí ela está truncada e há
/// mais dado do que ele viu.</para>
/// </summary>
internal sealed class LeitorAgenteSql(IFonteDados fonte, int maxLinhasPorPagina)
{
    public int MaxLinhasPorPagina { get; } = maxLinhasPorPagina;

    /// <summary>
    /// Executa e devolve as linhas já indexadas por nome de coluna (case-insensitive — o T-SQL
    /// devolve o nome como escrito no SELECT, e o Klinikos mistura <c>PROF_NOME</c> com
    /// <c>pac_nome</c> na mesma base).
    /// </summary>
    public async Task<IReadOnlyList<LinhaSql>> ConsultarAsync(string sql, CancellationToken ct)
    {
        var r = await fonte.ExecutarAsync(sql, ct, MaxLinhasPorPagina);
        if (!r.Sucesso)
            throw new ValidacaoException("pep.agente", r.Erro ?? "Consulta ao agente falhou sem mensagem.");

        var indice = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < r.Colunas.Count; i++) indice[r.Colunas[i]] = i;

        return [.. r.Linhas.Select(l => new LinhaSql(indice, l))];
    }

    /// <summary>
    /// Página encostou no teto? Então está truncada — há mais linhas que o conector não viu.
    /// Uma página truncada tratada como "acabou" é o modo de falhar mais perigoso que existe
    /// aqui: o run fecha como sucesso e a marca d'água avança por cima do que ficou de fora.
    /// </summary>
    public bool PaginaTruncada(int linhasRecebidas) => linhasRecebidas >= MaxLinhasPorPagina;
}

/// <summary>Uma linha de resultado, lida por nome de coluna e já convertida.</summary>
internal readonly struct LinhaSql(Dictionary<string, int> indice, IReadOnlyList<object?> valores)
{
    private object? Bruto(string coluna) =>
        indice.TryGetValue(coluna, out var i) && i < valores.Count ? valores[i] : null;

    /// <summary>Texto sem espaços de sobra; null quando vazio — o Klinikos usa <c>char(n)</c>
    /// em quase tudo, então praticamente todo valor vem com padding à direita.</summary>
    public string? Texto(string coluna) =>
        Bruto(coluna)?.ToString() is { } s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    public long? Numero(string coluna) => Bruto(coluna) switch
    {
        null => null,
        long l => l,
        int i => i,
        short s => s,
        decimal d => (long)d,
        double d => (long)d,
        var o => long.TryParse(o.ToString(), out var n) ? n : null,
    };

    /// <summary>
    /// Valor fracionário preservado. <see cref="Numero"/> devolve <c>long</c> e trunca — o que
    /// é inofensivo num rowversion e errado numa quantidade prescrita, onde "0,5 ampola" viraria
    /// zero. Aceita vírgula decimal porque o agente pode serializar no formato pt-BR.
    /// </summary>
    public decimal? Decimal(string coluna) => Bruto(coluna) switch
    {
        null => null,
        decimal d => d,
        double d => (decimal)d,
        long l => l,
        int i => i,
        short s => s,
        var o => decimal.TryParse(o.ToString()?.Replace(',', '.'),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null,
    };

    /// <summary>
    /// Data-hora em ISO local (<c>2026-08-03T21:12:00</c>), sem fuso — quem monta o recurso FHIR
    /// aplica o offset de Brasília. O agente serializa datas como texto ISO.
    /// </summary>
    public string? DataHora(string coluna) => Bruto(coluna) switch
    {
        null => null,
        DateTime dt => dt.ToString("yyyy-MM-dd'T'HH:mm:ss"),
        var o => o.ToString() is { Length: > 0 } s ? s.Trim() : null,
    };
}

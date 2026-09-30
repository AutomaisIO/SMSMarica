using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using SMSMais.Core.Common.Tempo;

namespace SMSMais.Core.Integracoes.SisregWeb.Indicadores;

/// <summary>Uma linha da lista oficial de faltas (<c>rel_amb_faltas_sol.pl</c>). Sem nome, endereço nem telefone.</summary>
public sealed record FaltaLidaSisreg(
    string Codigo, string? UnidadeSolicitante, DateOnly DataExecucao, string? Hora, string? Procedimento);

/// <summary>Uma marcação cancelada (<c>cons_marcacao_cancelada</c>). O operador fica só para a trilha da
/// conciliação — os indicadores não o gravam.</summary>
public sealed record MarcacaoCanceladaLidaSisreg(
    string Codigo, DateTime? CanceladoEm, DateOnly? DataMarcacao, string? Procedimento,
    string? Justificativa, string? Operador);

/// <summary>Uma cota da Consulta de PPI (<c>cons_ppi_cotas</c>), por procedimento.</summary>
public sealed record CotaPpiLidaSisreg(
    string? CodigoUnificado, string CodigoInterno, string? Procedimento, int Total, int Usada, int? Saldo, string Tipo);

/// <summary>Uma solicitação que saiu da fila sem agendamento (<c>gerenciador_solicitacao</c>, situação 3/4/6).</summary>
public sealed record DesfechoLidoSisreg(string Codigo, DateOnly? DataSolicitacao, string? Procedimento, string? Situacao);

/// <summary>Uma unidade solicitante do município, como o SISREG a lista.</summary>
public sealed record UnidadeSolicitanteSisreg(string Cnes, string Nome);

/// <summary>
/// Leitura das telas que alimentam os Indicadores de Regulação. Mesmo padrão dos outros parsers do
/// SISREG: regex sobre <c>tr</c>/<c>td</c>, sem DOM — as telas são tabelas planas.
///
/// <para><b>O que é "leitura completa".</b> Cada tela dá uma prova diferente, e sem prova nada é
/// gravado: a de canceladas e a do gerenciador DECLARAM o total (<c>PESQUISADAS (N)</c> /
/// <c>RETORNADAS (N)</c>); a de faltas não declara na lista inteira (<c>imprimir_lista=1</c>), mas a
/// versão paginada diz <c>Mostrando Página de N</c> (10 por página) — é o que confere a lista. Resposta
/// sem o cabeçalho da tela, sem <c>&lt;/html&gt;</c> ou com página de gateway (504) é inválida: o
/// proxy do SISREG corta consulta pesada aos ~65 s e às vezes entrega meia página.</para>
///
/// <para>Detalhe das telas: <c>Automais.SISREG/docs/APRENDIZADOS.md</c> §"Telas para INDICADORES DE
/// REGULAÇÃO".</para>
/// </summary>
public static partial class IndicadoresSisregHtmlParser
{
    /// <summary>Linhas por página da consulta de faltas SEM <c>imprimir_lista</c>.</summary>
    public const int FaltasPorPagina = 10;

    // --------------------------------------------------------------------------------- respostas

    /// <summary>A resposta chegou inteira? (fecha o HTML e não é a página de erro do gateway)</summary>
    public static bool Inteira(string? html) =>
        !string.IsNullOrEmpty(html)
        && html.Contains("</html>", StringComparison.OrdinalIgnoreCase)
        && !PaginaDeGateway(html);

    /// <summary>Página de erro do proxy (504/502): o SISREG não respondeu a tempo.</summary>
    public static bool PaginaDeGateway(string? html) =>
        !string.IsNullOrEmpty(html)
        && (html.Contains("Gateway Time-out", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Gateway Timeout", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Bad Gateway", StringComparison.OrdinalIgnoreCase)
            || GatewayTituloRegex().IsMatch(html));

    /// <summary>A tela diz, com todas as letras, que não há registro para o filtro.</summary>
    public static bool NenhumRegistro(string html)
    {
        var texto = Texto(html).ToLowerInvariant();
        return texto.Contains("nenhum registro", StringComparison.Ordinal)
               || texto.Contains("nenhuma solicita", StringComparison.Ordinal)
               || texto.Contains("nenhuma marca", StringComparison.Ordinal)
               || texto.Contains("nao foram encontrad", StringComparison.Ordinal)
               || texto.Contains("não foram encontrad", StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------------------ faltas

    /// <summary>É a Consulta de Absenteísmo (tem o cabeçalho da lista)?</summary>
    public static bool TelaDeFaltas(string html) =>
        Texto(html).Contains("Data de Execu", StringComparison.OrdinalIgnoreCase);

    /// <summary>"Mostrando Página de N" da consulta paginada — N páginas de <see cref="FaltasPorPagina"/>.</summary>
    public static int? PaginasDeFaltas(string html)
    {
        var m = MostrandoRegex().Match(Texto(html));
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>Colunas: código, unidade solicitante, paciente, endereço, telefone, data de execução,
    /// hora e procedimento — só as que não identificam o paciente são guardadas.</summary>
    public static List<FaltaLidaSisreg> Faltas(string html)
    {
        var fora = new List<FaltaLidaSisreg>();
        foreach (var c in Linhas(html, 8))
        {
            if (Data(c[5]) is not { } data) continue;
            fora.Add(new FaltaLidaSisreg(c[0], Vazio(c[1]), data, Vazio(c[6]), Vazio(c[7])));
        }
        return fora;
    }

    // -------------------------------------------------------------------------------- canceladas

    /// <summary>(linhas, páginas) declaradas pela Consulta de Marcações Canceladas.</summary>
    public static (int? Linhas, int? Paginas) TotalCanceladas(string html)
    {
        var n = PesquisadasRegex().Match(Texto(html));
        return (n.Success ? int.Parse(n.Groups[1].Value, CultureInfo.InvariantCulture) : null, Paginas(html));
    }

    /// <summary>
    /// As 9 colunas: código, data e hora da marcação, procedimento, profissional, paciente,
    /// justificativa, operador e o instante do cancelamento (Brasília, "dd.MM.yyyy HH:mm:ss").
    /// </summary>
    public static List<MarcacaoCanceladaLidaSisreg> Canceladas(string html)
    {
        var fora = new List<MarcacaoCanceladaLidaSisreg>();
        foreach (var c in Linhas(html, 9, CodigoCurtoRegex()))
            fora.Add(new MarcacaoCanceladaLidaSisreg(c[0], Instante(c[8]), Data(c[1]), Vazio(c[3]), Vazio(c[6]), Vazio(c[7])));
        return fora;
    }

    // ------------------------------------------------------------------------------------- PPI

    /// <summary>É a Consulta de PPI com a tabela de procedimentos?</summary>
    public static bool TelaDePpi(string html) =>
        Texto(html).Contains("PPI por Procedimento", StringComparison.OrdinalIgnoreCase);

    /// <summary>Colunas: código unificado, interno, procedimento, total, usada, saldo, tipo (e o link de detalhe).</summary>
    public static List<CotaPpiLidaSisreg> Ppi(string html)
    {
        var fora = new List<CotaPpiLidaSisreg>();
        foreach (Match tr in LinhaRegex().Matches(html))
        {
            var c = Celulas(tr.Groups[1].Value);
            if (c.Count < 7 || string.IsNullOrEmpty(c[1])
                || !int.TryParse(c[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var total)
                || !int.TryParse(c[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var usada))
            {
                continue;
            }
            int? saldo = int.TryParse(c[5], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var s) ? s : null;
            fora.Add(new CotaPpiLidaSisreg(Vazio(c[0]), c[1], Vazio(c[2]), total, usada, saldo, c[6]));
        }
        return fora;
    }

    // ------------------------------------------------------------------------------- desfechos

    /// <summary>"SOLICITAÇÕES RETORNADAS (N)" do gerenciador.</summary>
    public static int? Retornadas(string html)
    {
        var m = RetornadasRegex().Match(Texto(html));
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>Colunas usadas: código (0), data da solicitação (1), procedimento (7) e situação (11, ex.: "SOL/DEV/REG").</summary>
    public static List<DesfechoLidoSisreg> Desfechos(string html)
    {
        var fora = new List<DesfechoLidoSisreg>();
        foreach (var c in Linhas(html, 12))
            fora.Add(new DesfechoLidoSisreg(c[0], Data(c[1]), Vazio(c[7]), Vazio(c[11])));
        return fora;
    }

    /// <summary>As unidades solicitantes do <c>select unidade_adm</c> da tela de negados/devolvidos.</summary>
    public static List<UnidadeSolicitanteSisreg> UnidadesSolicitantes(string html)
    {
        var select = SelectUnidadeRegex().Match(html);
        if (!select.Success) return [];
        var fora = new List<UnidadeSolicitanteSisreg>();
        foreach (Match o in OpcaoRegex().Matches(select.Groups[1].Value))
        {
            var cnes = o.Groups[1].Value.Trim();
            if (!CnesRegex().IsMatch(cnes)) continue;
            fora.Add(new UnidadeSolicitanteSisreg(cnes, Limpar(o.Groups[2].Value)));
        }
        return fora;
    }

    // -------------------------------------------------------------------------------- internos

    private static int? Paginas(string html)
    {
        var p = ExibirPaginaRegex().Matches(html);
        return p.Count > 0 ? p.Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Max() : null;
    }

    private static IEnumerable<List<string>> Linhas(string html, int minimo, Regex? codigo = null)
    {
        codigo ??= CodigoRegex();
        foreach (Match tr in LinhaRegex().Matches(html))
        {
            var c = Celulas(tr.Groups[1].Value);
            if (c.Count >= minimo && codigo.IsMatch(c[0])) yield return c;
        }
    }

    private static List<string> Celulas(string tr) =>
        CelulaRegex().Matches(tr).Select(td => Limpar(td.Groups[1].Value)).ToList();

    private static string Limpar(string fragmento) =>
        EspacosRegex().Replace(WebUtility.HtmlDecode(TagRegex().Replace(fragmento, " ")), " ").Trim();

    private static string Texto(string html) => EspacosRegex().Replace(WebUtility.HtmlDecode(TagRegex().Replace(html, " ")), " ");

    private static string? Vazio(string s) => string.IsNullOrWhiteSpace(s) || s == "---" ? null : s;

    /// <summary>"dd/MM/yyyy" (ou "dd.MM.yyyy", como aparece em algumas colunas).</summary>
    private static DateOnly? Data(string texto)
    {
        var t = texto.Trim();
        if (t.Length > 10) t = t[..10];
        return DateOnly.TryParseExact(t, ["dd/MM/yyyy", "dd.MM.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;
    }

    /// <summary>"18.09.2026 11:24:54" (Brasília) → instante UTC.</summary>
    private static DateTime? Instante(string texto) =>
        DateTime.TryParseExact(texto.Trim(), ["dd.MM.yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? FusoBrasilia.DeBrasiliaParaUtc(local)
            : null;

    [GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex LinhaRegex();

    [GeneratedRegex(@"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CelulaRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosRegex();

    /// <summary>Código de solicitação do SISREG (9–10 dígitos na prática).</summary>
    [GeneratedRegex(@"^\d{8,12}$")]
    private static partial Regex CodigoRegex();

    /// <summary>A tela de canceladas aceita o formato que a conciliação sempre aceitou (6+ dígitos).</summary>
    [GeneratedRegex(@"^\d{6,}$")]
    private static partial Regex CodigoCurtoRegex();

    [GeneratedRegex(@"^\d{7}$")]
    private static partial Regex CnesRegex();

    [GeneratedRegex(@"PESQUISADAS?\s*\((\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex PesquisadasRegex();

    [GeneratedRegex(@"RETORNADAS\s*\((\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex RetornadasRegex();

    [GeneratedRegex(@"exibirPagina\(\s*[^,]+,\s*(\d+)\s*\)")]
    private static partial Regex ExibirPaginaRegex();

    [GeneratedRegex(@"Mostrando\s+P\S*gina\s+de\s+(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex MostrandoRegex();

    [GeneratedRegex(@"<title>\s*50[234]\b", RegexOptions.IgnoreCase)]
    private static partial Regex GatewayTituloRegex();

    [GeneratedRegex(@"<select[^>]*name\s*=\s*['""]?unidade_adm['""]?[^>]*>(.*?)</select>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SelectUnidadeRegex();

    [GeneratedRegex(@"<option[^>]*value\s*=\s*['""]?([^'"" >]*)['""]?[^>]*>(.*?)</option>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex OpcaoRegex();
}

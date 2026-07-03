using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace SMSMarica.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Parser da tela "Agendados pela Regulação" (<c>cons_marcados_reg</c>) do SISREG III.
/// Duas etapas: a LISTAGEM (<c>table_listagem</c>, 11 colunas) dá os códigos + paginação;
/// a FICHA (<c>EXIBIR_FICHA</c>) dá os detalhes (paciente, médico, unidades, data/hora).
///
/// A ficha é um grid onde uma sequência de rótulos (terminados em ':') é seguida pela
/// sequência de valores correspondentes — pareamos por posição dentro do grupo.
/// </summary>
public static partial class MarcadosRegParser
{
    public sealed record LinhaListagem(string CodigoSolicitacao, string Procedimento, string UnidadeExecutante, string DataExecucao, string HoraExecucao);

    public sealed record Listagem(IReadOnlyList<LinhaListagem> Linhas, int TotalPaginas, int? TotalRegistros);

    /// <summary>Extrai as linhas de dados (11 colunas) + total de páginas + total pesquisado.</summary>
    public static Listagem ParseListagem(string html)
    {
        var doc = new HtmlParser().ParseDocument(html);
        var linhas = new List<LinhaListagem>();
        foreach (var tabela in doc.QuerySelectorAll("table.table_listagem"))
        {
            foreach (var tr in tabela.QuerySelectorAll("tr"))
            {
                var c = Celulas(tr);
                if (c.Count != 11) continue;
                var cod = c[0].Trim();
                if (cod.Length == 0 || !cod.All(char.IsDigit)) continue; // pula cabeçalho
                linhas.Add(new LinhaListagem(cod, c[5], c[7], c[8], c[9]));
            }
        }

        // Total de registros: "...PESQUISADAS (336)".
        int? total = null;
        var mTot = TotalRegex().Match(html);
        if (mTot.Success) total = int.Parse(mTot.Groups[1].Value, CultureInfo.InvariantCulture);

        // Páginas: a paginação do SISREG é via JS (exibirPagina), NÃO há "de N <A HREF".
        // Fonte confiável = ceil(total / 10). Fallback: nº de páginas do regex antigo, senão 1.
        const int PorPagina = 10;
        int totalPag;
        var mPag = PaginasRegex().Match(html);
        if (total is { } t && t > 0) totalPag = (int)Math.Ceiling(t / (double)PorPagina);
        else if (mPag.Success) totalPag = int.Parse(mPag.Groups[1].Value, CultureInfo.InvariantCulture);
        else totalPag = linhas.Count > 0 ? 1 : 0;

        return new Listagem(linhas, Math.Max(totalPag, linhas.Count > 0 ? 1 : 0), total);
    }

    /// <summary>Interpreta a ficha detalhe. Preenche o que existir; campos ausentes ficam null.</summary>
    public static MarcacaoSisreg? ParseFicha(string html, string codigoSolicitacao)
    {
        if (string.IsNullOrWhiteSpace(html) || CadsusHtmlParser.SessaoInvalida(html)) return null;

        var doc = new HtmlParser().ParseDocument(html);
        var cs = doc.QuerySelectorAll("td, th")
            .Select(c => EspacosRegex().Replace(c.TextContent, " ").Trim())
            .Where(t => t.Length > 0)
            .ToList();
        if (cs.Count == 0) return null;

        // CNS do paciente: primeiro "CNS:" após "Dados do Paciente".
        string? cns = null;
        var iPac = cs.IndexOf("Dados do Paciente");
        if (iPac >= 0)
        {
            for (var k = iPac; k < Math.Min(iPac + 8, cs.Count); k++)
            {
                if (cs[k] == "CNS:" && k + 1 < cs.Count && Digitos(cs[k + 1]).Length == 15) { cns = Digitos(cs[k + 1]); break; }
            }
        }

        // Nome do paciente: primeiro valor "gritado" (maiúsculo, sem ':') após "Nome do Paciente".
        string? nomePac = null;
        var iNome = cs.IndexOf("Nome do Paciente");
        if (iNome >= 0)
        {
            for (var j = iNome + 1; j < Math.Min(iNome + 8, cs.Count); j++)
            {
                if (!cs[j].EndsWith(':') && cs[j].Length > 5 && cs[j] == cs[j].ToUpperInvariant() && cs[j].Any(char.IsLetter)) { nomePac = cs[j]; break; }
            }
        }

        var (nSol, cnesSol) = BlocoUnidade(cs, "UNIDADE SOLICITANTE");
        var (nExe, cnesExe) = BlocoUnidade(cs, "UNIDADE EXECUTANTE");

        var medCpf = Digitos(GridValor(cs, "CPF do Médico Solicitante:"));
        var medNome = LimparNulo(GridValor(cs, "Nome Médico Solicitante:"));
        var medCrm = LimparNulo(GridValor(cs, "CRM:"));
        var cid = LimparNulo(GridValor(cs, "CID:"));

        var dataHora = ExtrairDataHora(cs);

        return new MarcacaoSisreg(
            CodigoSolicitacao: codigoSolicitacao,
            CnsPaciente: cns,
            NomePaciente: nomePac,
            ProcedimentoTexto: null, // vem da listagem (a ficha lista códigos internos); preenchido pelo scraper.
            CodigoSigtap: null,
            CpfMedicoSolicitante: medCpf.Length == 11 ? medCpf : null,
            NomeMedicoSolicitante: medNome,
            CrmMedicoSolicitante: medCrm,
            CnesUnidadeSolicitante: cnesSol,
            NomeUnidadeSolicitante: nSol,
            CnesUnidadeExecutante: cnesExe,
            NomeUnidadeExecutante: nExe,
            DataHoraAtendimento: dataHora,
            DataSolicitacao: null, // a lista "Agendados pela Regulação" não traz a data da solicitação; só o TXT.
            DataRegulacao: null,
            Cid: cid);
    }

    /// <summary>Bloco "UNIDADE SOLICITANTE/EXECUTANTE": nome = 1º valor; CNES = valor de 7 dígitos.</summary>
    private static (string? nome, string? cnes) BlocoUnidade(List<string> cs, string header)
    {
        var i = cs.IndexOf(header);
        if (i < 0) return (null, null);
        var labels = new List<string>();
        var j = i + 1;
        while (j < cs.Count && cs[j].EndsWith(':')) { labels.Add(cs[j]); j++; }
        var vals = cs.Skip(j).Take(labels.Count).ToList();
        var nome = vals.Count > 0 ? LimparNulo(vals[0]) : null;
        var cnes = vals.FirstOrDefault(v => v.Length == 7 && v.All(char.IsDigit));
        return (nome, cnes);
    }

    /// <summary>Valor no grid: acha o grupo de rótulos consecutivos e retorna o valor na MESMA posição.</summary>
    private static string? GridValor(List<string> cs, string label)
    {
        for (var i = 0; i < cs.Count; i++)
        {
            if (cs[i] != label) continue;
            var inicio = i;
            while (inicio > 0 && cs[inicio - 1].EndsWith(':')) inicio--;
            var grupo = new List<string>();
            var k = inicio;
            while (k < cs.Count && cs[k].EndsWith(':')) { grupo.Add(cs[k]); k++; }
            var idx = grupo.IndexOf(label);
            var vals = cs.Skip(k).Take(grupo.Count).ToList();
            return idx >= 0 && idx < vals.Count ? vals[idx] : null;
        }
        return null;
    }

    /// <summary>Data/hora de atendimento: procura "dd/mm/aaaa ... HHhMMmin" ou "dd/mm/aaaa - SEX - HH:MM".</summary>
    private static DateTime? ExtrairDataHora(List<string> cs)
    {
        foreach (var t in cs)
        {
            var m = DataHoraRegex().Match(t);
            if (!m.Success) continue;
            var data = m.Groups[1].Value;
            var hh = m.Groups[2].Value.PadLeft(2, '0');
            var mm = m.Groups[3].Value.PadLeft(2, '0');
            if (DateTime.TryParseExact($"{data} {hh}:{mm}", "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
        }
        return null;
    }

    private static List<string> Celulas(AngleSharp.Dom.IElement tr) =>
        [.. tr.QuerySelectorAll("td, th").Select(c => EspacosRegex().Replace(c.TextContent, " ").Trim())];

    private static string Digitos(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string([.. s.Where(char.IsDigit)]);

    private static string? LimparNulo(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        return t.Length == 0 || t.Trim('-', ' ', '.').Length == 0 ? null : t;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosRegex();

    [GeneratedRegex(@"de\s+(\d+)\s*<A HREF", RegexOptions.IgnoreCase)]
    private static partial Regex PaginasRegex();

    [GeneratedRegex(@"PESQUISADAS\s*\((\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex TotalRegex();

    // "01/07/2026 ... 08h00min"  ou  "01/07/2026 - QUA - 08:00"
    [GeneratedRegex(@"(\d{2}/\d{2}/\d{4}).{0,12}?(\d{1,2})[h:](\d{2})")]
    private static partial Regex DataHoraRegex();
}

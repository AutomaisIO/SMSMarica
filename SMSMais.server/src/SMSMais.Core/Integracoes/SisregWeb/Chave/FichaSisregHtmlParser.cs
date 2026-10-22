using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace SMSMais.Core.Integracoes.SisregWeb.Chave;

/// <summary>Um par rótulo → valor da ficha, como o SISREG escreve.</summary>
public sealed record CampoFichaSisreg(string Rotulo, string Valor);

/// <summary>Uma seção da ficha ("UNIDADE EXECUTANTE", "Dados do Paciente"…).</summary>
/// <param name="Textos">Texto corrido da seção que não está em par rótulo/valor (ex.: avisos do município).</param>
public sealed record SecaoFichaSisreg(string Titulo, IReadOnlyList<CampoFichaSisreg> Campos, IReadOnlyList<string> Textos);

/// <summary>A ficha da solicitação — o que o "Imprimir" do SISREG imprime.</summary>
public sealed record FichaSisreg(
    string? Chave,
    string? CodigoSolicitacao,
    IReadOnlyList<SecaoFichaSisreg> Secoes,
    string? ExtraidaEm)
{
    /// <summary>O valor de um rótulo (primeira ocorrência, em qualquer seção), sem acento nem caixa.</summary>
    public string? Valor(string rotulo) => Secoes.SelectMany(s => s.Campos)
        .FirstOrDefault(c => FichaSisregHtmlParser.Normalizar(c.Rotulo) == FichaSisregHtmlParser.Normalizar(rotulo))?.Valor;
}

/// <summary>
/// Lê a <c>div.fichaAmbulatorial</c> da ficha do SISREG (<c>cons_marcados_reg</c> EXIBIR_FICHA ou
/// <c>gerenciador_solicitacao</c> VISUALIZAR_FICHA) — a guia do agendamento (ADR-0076).
///
/// <para><b>Genérico de propósito</b>: a ficha é uma tabela com título de seção
/// (<c>td.td_titulo_tabela</c>), linha de rótulos (<c>tr bgcolor=#F3F3F3</c>) e, logo abaixo, a linha
/// de valores com a mesma sequência de células. Ler por essa regra, e não campo a campo, faz a guia
/// sair inteira mesmo quando o SISREG acrescenta um campo. Rótulos se repetem entre seções (CNES,
/// Telefone, Bairro…), por isso o par fica dentro da seção.</para>
///
/// <para>O <c>tbody.FichaReduzida</c> é a visão resumida, escondida (<c>display:none</c>) e
/// malformada — fica de fora; a impressão do SISREG sai com a completa.</para>
/// </summary>
public static partial class FichaSisregHtmlParser
{
    /// <summary>A ficha, ou <c>null</c> quando a página não a traz (outra tela, sessão caída, miolo vazio).</summary>
    public static FichaSisreg? Ler(string? html)
    {
        if (string.IsNullOrWhiteSpace(html) || !html.Contains("fichaAmbulatorial", StringComparison.Ordinal)) return null;

        var doc = new HtmlParser().ParseDocument(html);
        var raiz = doc.GetElementById("fichaAmbulatorial") ?? doc.QuerySelector(".fichaAmbulatorial");
        var tabela = raiz?.QuerySelector("table");
        if (tabela is null) return null;

        var secoes = new List<(string Titulo, List<CampoFichaSisreg> Campos, List<string> Textos)>();
        (string Titulo, List<CampoFichaSisreg> Campos, List<string> Textos) atual = ("", [], []);
        secoes.Add(atual);
        List<string>? rotulos = null;

        foreach (var tr in tabela.QuerySelectorAll("tr"))
        {
            if (tr.Closest("tbody")?.ClassList.Contains("FichaReduzida") == true) continue;
            var celulas = tr.Children.Where(c => c.LocalName is "td" or "th").ToList();
            if (celulas.Count == 0) continue;

            if (celulas.Any(c => c.ClassList.Contains("td_titulo_tabela")))
            {
                atual = (Texto(celulas[0]), [], []);
                secoes.Add(atual);
                rotulos = null;
                continue;
            }

            if (EhLinhaDeRotulos(tr, celulas))
            {
                rotulos = celulas.Select(Rotulo).ToList();
                continue;
            }

            if (rotulos is not null)
            {
                for (var i = 0; i < celulas.Count && i < rotulos.Count; i++)
                {
                    var valor = Valor(celulas[i]);
                    if (rotulos[i].Length > 0 && valor is not null) atual.Campos.Add(new CampoFichaSisreg(rotulos[i], valor));
                }
                continue;
            }

            var texto = Texto(tr);
            if (texto.Length > 0) atual.Textos.Add(texto);
        }

        var lista = secoes
            .Where(s => s.Campos.Count > 0 || s.Textos.Count > 0)
            .Select(s => new SecaoFichaSisreg(s.Titulo, s.Campos, s.Textos))
            .ToList();
        // A primeira "seção" sem título é a da chave (rótulo e valor fora do padrão de cinza).
        lista = [.. lista.Where(s => !(s.Titulo.Length == 0 && s.Campos.Count == 0
                                      && s.Textos.Any(t => Normalizar(t).StartsWith("chave de confirmacao", StringComparison.Ordinal))))];

        var extraida = raiz!.QuerySelectorAll("p")
            .Select(p => Texto(p))
            .FirstOrDefault(t => Normalizar(t).StartsWith("data da extracao", StringComparison.Ordinal));

        var ficha = new FichaSisreg(FichaChaveHtmlParser.Ler(html), null, lista, extraida);
        return ficha with { CodigoSolicitacao = ficha.Valor("Código da Solicitação")?.Split(' ')[0] };
    }

    private static bool EhLinhaDeRotulos(IElement tr, List<IElement> celulas) =>
        string.Equals(tr.GetAttribute("bgcolor"), "#F3F3F3", StringComparison.OrdinalIgnoreCase)
        && celulas.All(c => c.QuerySelector("b") is not null || Texto(c).Length == 0);

    private static string Rotulo(IElement celula)
    {
        var b = celula.QuerySelector("b");
        var texto = Texto(b ?? celula);
        texto = RegexParenteseLink().Replace(texto, string.Empty).Trim();
        return texto.TrimEnd(':').Trim();
    }

    private static string? Valor(IElement celula)
    {
        var texto = RegexParenteseLink().Replace(Texto(celula), string.Empty)
            .Replace('●', '·')
            .Trim();
        texto = RegexEspacos().Replace(texto, " ");
        return texto.Length == 0 || texto == "---" ? null : texto;
    }

    /// <summary>Texto do elemento: entidades decodificadas (o AngleSharp já faz), <c>&lt;br&gt;</c> e
    /// blocos viram espaço, espaços colapsados.</summary>
    private static string Texto(IElement elemento)
    {
        var texto = elemento.TextContent.Replace(' ', ' ');
        return RegexEspacos().Replace(texto, " ").Trim();
    }

    /// <summary>Sem acento, minúsculo, espaços simples — para casar rótulos.</summary>
    internal static string Normalizar(string s)
    {
        var d = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(d.Length);
        foreach (var ch in d)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(ch));
        }
        return RegexEspacos().Replace(sb.ToString(), " ").Trim().TrimEnd(':');
    }

    /// <summary>"(Exibir Histórico)", "(Exibir Lista Detalhada)" — links da tela, não dado.</summary>
    [GeneratedRegex(@"\(\s*Exibir[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexParenteseLink();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacos();
}

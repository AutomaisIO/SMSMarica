using System.Net;
using System.Text.RegularExpressions;

namespace SMSMarica.Core.Integracoes.SisregWeb.Mapeamento;

/// <summary>
/// Parser das respostas do <c>/cgi-bin/sisreg_ajax</c>, que devolve XML no formato
/// <c>&lt;ROW codigo="X"&gt;DESCRIÇÃO&lt;/ROW&gt;</c>.
///
/// <para>Usado para as duas listas encadeadas do mapeamento:
/// <c>PROFISSIONAIS_POR_UPS</c> (código = CPF) e
/// <c>PROCEDIMENTOS_POR_PROFISSIONAIS_E_UPS</c> (código = procedimento SIGTAP).</para>
///
/// <para><b>Cuidado:</b> a primeira linha costuma ser o placeholder do combo
/// ("Selecione o Profissional") com <c>codigo</c> vazio — descartada aqui. E um
/// <c>&lt;ROOT/&gt;</c> vazio não significa "não há dados": significa quase sempre sessão
/// derrubada ou anti-bot, tratado por quem chama.</para>
/// </summary>
public static partial class SisregAjaxParser
{
    public sealed record Linha(string Codigo, string Descricao);

    public static IReadOnlyList<Linha> LerLinhas(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return [];

        var linhas = new List<Linha>();
        foreach (Match m in RegexRow().Matches(xml))
        {
            var codigo = m.Groups["codigo"].Value.Trim();
            if (codigo.Length == 0) continue; // placeholder do combo

            var descricao = WebUtility.HtmlDecode(m.Groups["descricao"].Value).Trim();
            descricao = RegexEspacos().Replace(descricao, " ");
            linhas.Add(new Linha(codigo, descricao));
        }
        return linhas;
    }

    [GeneratedRegex("""<ROW\s+codigo\s*=\s*"(?<codigo>[^"]*)"\s*>(?<descricao>.*?)</ROW>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexRow();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacos();
}

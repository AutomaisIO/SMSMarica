using System.Net;
using System.Text.RegularExpressions;

namespace SMSMais.Core.Integracoes.SisregWeb.Chave;

/// <summary>
/// Tira a <b>chave de confirmação</b> da ficha de uma solicitação do SISREG.
///
/// <para>Na ficha do <c>cons_marcados_reg</c> (<c>etapa=EXIBIR_FICHA</c>) ela é o primeiro campo,
/// em fonte grande, antes da unidade solicitante — capturas do laboratório
/// (<c>Automais.SISREG/capturas/ficha_*.html</c>, linha 210):</para>
/// <code>
/// &lt;td&gt;&lt;b&gt;Chave de Confirma&amp;#231;&amp;#227;o:&lt;/b&gt;&lt;/td&gt;&lt;/tr&gt;
/// &lt;tr&gt;&lt;td style='... font-size: 180%;'&gt;&lt;b&gt;98626&lt;/b&gt;&lt;/td&gt;
/// </code>
///
/// <para><b>O rótulo vem com entidade</b> (<c>&amp;#231;&amp;#227;</c>): procurar "Confirmação"
/// no HTML cru não acha nada. Por isso o HTML é decodificado antes do regex.</para>
///
/// <para>O valor é o <b>primeiro negrito depois do rótulo</b>, sem outro negrito no meio — se a
/// célula vier vazia, o regex não pode escorregar para o próximo campo da ficha e devolver, por
/// exemplo, um CNES como chave.</para>
/// </summary>
public static partial class FichaChaveHtmlParser
{
    [GeneratedRegex(
        @"Chave\s+de\s+Confirma[çc][ãa]o\s*:?\s*</b>(?:(?!<b[\s>]).){0,400}?<b>\s*([A-Za-z0-9]{3,20})\s*</b>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ChaveRegex();

    /// <summary>A chave, ou <c>null</c> quando a página não a mostra (outra tela, sem acesso,
    /// solicitação ainda não autorizada, sessão caída).</summary>
    public static string? Ler(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var m = ChaveRegex().Match(WebUtility.HtmlDecode(html));
        return m.Success ? m.Groups[1].Value : null;
    }
}

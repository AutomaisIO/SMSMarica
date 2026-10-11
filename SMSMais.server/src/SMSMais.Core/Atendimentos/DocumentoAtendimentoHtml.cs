using System.Text;
using System.Text.RegularExpressions;
using Hl7.Fhir.Model;

namespace SMSMais.Core.Atendimentos;

/// <summary>
/// Conteúdo de um documento de atendimento (<c>DocumentReference</c>) como HTML pronto para exibir
/// — no painel, no PWA e no app nativo, que desenham com o mesmo padrão visual (classes <c>edoc-*</c>).
///
/// <para>Os importadores entregam três formas: o eDoc do Salux (HTML com <c>edoc-campo</c>), o
/// Boletim Médico do Klinikos (<c>h3</c> + <c>p</c>) e a evolução do Klinikos, que é
/// <b>texto puro</b>. As duas primeiras já são HTML e passam como estão. A terceira era exibida como
/// se fosse HTML: as quebras de linha sumiam (virava um bloco só) e um "&lt;" digitado no prontuário
/// virava marcação. Aqui ela é escapada e vira parágrafos.</para>
/// </summary>
public static partial class DocumentoAtendimentoHtml
{
    public static string ParaExibir(Attachment? anexo)
    {
        if (anexo?.Data is not { } bytes) return string.Empty;
        var conteudo = Encoding.UTF8.GetString(bytes);
        return EhTextoPuro(anexo.ContentType) ? DeTextoPuro(conteudo) : conteudo;
    }

    /// <summary>
    /// Texto puro → <c>&lt;div class="edoc-texto"&gt;</c> com um parágrafo por bloco (linha em branco
    /// separa bloco) e <c>&lt;br/&gt;</c> nas quebras simples, que no prontuário costumam separar
    /// itens ("PA: …", "FC: …"). Texto vazio devolve vazio — a lista do app esconde o documento.
    /// </summary>
    public static string DeTextoPuro(string texto)
    {
        var normalizado = texto.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        if (normalizado.Length == 0) return string.Empty;

        var html = new StringBuilder("<div class=\"edoc-texto\">");
        foreach (var bloco in RegexLinhaEmBranco().Split(normalizado))
        {
            var linhas = bloco.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).Select(Escapar);
            html.Append("<p>").AppendJoin("<br/>", linhas).Append("</p>");
        }
        return html.Append("</div>").ToString();
    }

    private static bool EhTextoPuro(string? contentType) =>
        contentType is not null
        && contentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase);

    private static string Escapar(string s) => s
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

    [GeneratedRegex(@"\n[ \t]*\n\s*")]
    private static partial Regex RegexLinhaEmBranco();
}

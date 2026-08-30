using System.Net;
using System.Text.RegularExpressions;

namespace SMSMais.Core.Integracoes.SisregWeb.Unidades;

/// <summary>
/// Lê o combo de unidades executantes (<c>&lt;select name="ups"&gt;</c>) do formulário
/// <c>/cgi-bin/cons_agendas</c> — a lista de <b>todas</b> as unidades que a credencial enxerga.
///
/// <para><b>Por que esta tela:</b> a descoberta precisa custar quase nada. O formulário do
/// <c>cons_agendas</c> vem inteiro num único GET e já traz o par CNES → nome de cada unidade da
/// rede (42 em Maricá, medido em <c>Automais.SISREG/docs/APRENDIZADOS.md</c>). O
/// <c>cons_unidade</c> devolveria mais campos (telefone, IBGE, tipo), mas é paginado e é uma tela
/// de resultado — mais requisições e um HTML muito mais frágil de raspar, para enriquecer um
/// cadastro que o resto do sistema já sabe completar por CNES.</para>
///
/// <para><b>Formato real</b> (captura <c>_cons_agendas_programador.html</c>): tags em CAIXA ALTA,
/// atributos com aspas duplas, sem acento nos nomes, e a primeira opção é o placeholder
/// <c>&lt;OPTION value=""&gt;Selecione a unidade&lt;/OPTION&gt;</c>. Por isso tudo aqui é
/// <c>IgnoreCase</c> e opção sem CNES de 7 dígitos é descartada.</para>
/// </summary>
public static partial class ConsAgendasUpsParser
{
    /// <summary>Uma unidade como o SISREG a lista: CNES (7 dígitos) e nome fantasia em caixa alta.</summary>
    public sealed record UnidadeSisreg(string Cnes, string Nome);

    /// <summary>
    /// Extrai as unidades do HTML do formulário. Lista vazia significa <b>não confie</b>: ou a
    /// sessão caiu, ou a tela mudou — quem chama trata como falha de descoberta, nunca como
    /// "a rede não tem unidades".
    /// </summary>
    public static IReadOnlyList<UnidadeSisreg> Ler(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];

        var select = RegexSelectUps().Match(html);
        if (!select.Success) return [];

        var unidades = new List<UnidadeSisreg>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match opcao in RegexOpcao().Matches(select.Groups["corpo"].Value))
        {
            var cnes = new string([.. opcao.Groups["cnes"].Value.Where(char.IsDigit)]);
            if (cnes.Length != 7) continue; // placeholder "Selecione a unidade" e lixo

            var nome = WebUtility.HtmlDecode(opcao.Groups["nome"].Value);
            nome = RegexTags().Replace(nome, " ");
            nome = RegexEspacos().Replace(nome, " ").Trim();
            if (nome.Length == 0) continue;

            // O SISREG repete unidade no combo de vez em quando; o CNES é a chave.
            if (vistos.Add(cnes)) unidades.Add(new UnidadeSisreg(cnes, nome.ToUpperInvariant()));
        }

        return unidades;
    }

    [GeneratedRegex("""<select\b[^>]*\bname\s*=\s*["']?ups["']?[^>]*>(?<corpo>.*?)</select>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexSelectUps();

    [GeneratedRegex("""<option\b[^>]*\bvalue\s*=\s*["']?(?<cnes>[^"'>\s]*)["']?[^>]*>(?<nome>.*?)</option>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexOpcao();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex RegexTags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacos();
}

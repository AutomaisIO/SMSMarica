using System.Net;
using System.Text.RegularExpressions;

namespace SMSMais.Core.Integracoes.SisregWeb.Cancelamento;

/// <summary>
/// Lê da tela de cancelamento do SISREG as três coisas que importam: a situação da ficha, o
/// checkbox exato de uma linha e os campos de paginação.
/// </summary>
internal static partial class FichaCancelamentoHtmlParser
{
    /// <summary>
    /// A situação declarada na ficha — <c>AGENDAMENTO / CANCELADO / REGULADOR</c>,
    /// <c>SOLICITAÇÃO / AUTORIZADA / REGULADOR</c> — ou <c>null</c> quando a ficha não abriu.
    ///
    /// <para><b>Duas armadilhas, as duas já pagas.</b> A primeira: a página escreve os acentos
    /// como entidades (<c>Situa&amp;ccedil;&amp;atilde;o Atual</c>), então procurar por "Situação"
    /// sem desfazê-las não casa nunca. A segunda: a ficha é uma tabela com uma linha de RÓTULOS e a
    /// seguinte com os VALORES, então ler logo depois de "Situação Atual:" devolve o rótulo vizinho
    /// ("Data de Cancelamento"), não o valor. A âncora confiável é o próprio código, que na linha de
    /// valores vem imediatamente antes da situação.</para>
    /// </summary>
    public static string? Situacao(string html, string codigo)
    {
        if (string.IsNullOrEmpty(html)) return null;

        var texto = Texto(html);
        var m = Regex.Match(texto, $@"\b{Regex.Escape(codigo)}\s+([A-ZÇÃÕÉÍÁÚ][A-ZÇÃÕÉÍÁÚ/ ]{{4,70}})");
        if (!m.Success) return null;

        // O texto corrido não tem fronteira: depois da situação já vem o rótulo seguinte ("CPF do
        // Médico Solicitante"), e ele entra no casamento. A situação tem a forma FASE / ESTADO /
        // QUEM — ficam três partes, e da última só a primeira palavra.
        var partes = m.Groups[1].Value
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(3)
            .ToArray();
        if (partes.Length == 0) return null;
        partes[^1] = partes[^1].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? partes[^1];
        return string.Join(" / ", partes);
    }

    /// <summary>
    /// O checkbox da linha daquele código — nome e valor <b>lidos do HTML</b>, nunca construídos.
    ///
    /// <para>Só aceita quando o <c>value</c> é exatamente o código procurado. <c>chk_N</c> mistura
    /// a posição da linha na página com o código da solicitação, e não se sabe qual dos dois o CGI
    /// obedece — montar o nome à mão poderia cancelar o exame de outra pessoa.</para>
    /// </summary>
    public static (string Campo, string Valor)? Checkbox(string html, string codigo)
    {
        if (string.IsNullOrEmpty(html)) return null;

        foreach (Match tag in InputRegex().Matches(html))
        {
            var texto = tag.Value;
            if (!texto.Contains("checkbox", StringComparison.OrdinalIgnoreCase)) continue;

            var nome = Regex.Match(texto, @"name=[""']?(chk_\d+)[""']?", RegexOptions.IgnoreCase);
            var valor = Regex.Match(texto, @"value=[""']?(\d+)[""']?", RegexOptions.IgnoreCase);
            if (nome.Success && valor.Success && valor.Groups[1].Value == codigo)
                return (nome.Groups[1].Value, valor.Groups[1].Value);
        }
        return null;
    }

    /// <summary>
    /// O CNS do paciente, lido da própria ficha.
    ///
    /// <para>Existe porque a tela de cancelamento busca por CNS e 15% dos nossos agendamentos
    /// futuros (2.992 de 19.404, medido em 20/09/2026) são de pacientes sem CNS no cadastro —
    /// exigi-lo recusaria uma em cada sete tentativas de cancelamento. A ficha já é aberta para
    /// conferir a situação; o CNS vem de graça na mesma resposta, e vem do SISREG, que é a fonte
    /// que a própria tela vai consultar.</para>
    /// </summary>
    public static string? Cns(string html)
    {
        if (string.IsNullOrEmpty(html)) return null;
        // "CNS:" e, logo depois, os 15 dígitos — a ficha põe rótulo e valor em linhas distintas
        // da tabela, então o que separa os dois é só espaço no texto plano.
        var m = Regex.Match(Texto(html), @"CNS:?\s*(\d{15})", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Os hidden <c>total</c> e <c>ordem</c>, que a paginação precisa ecoar.</summary>
    public static (string? Total, string? Ordem) TotalEOrdem(string html)
    {
        var t = Regex.Match(html, @"name=[""']?total[""']?[^>]*value=[""']?(\d*)", RegexOptions.IgnoreCase);
        var o = Regex.Match(html, @"name=[""']?ordem[""']?[^>]*value=[""']?(\d*)", RegexOptions.IgnoreCase);
        return (t.Success ? t.Groups[1].Value : null, o.Success ? o.Groups[1].Value : null);
    }

    private static string Texto(string html)
    {
        var sem = Regex.Replace(html, @"<script.*?</script>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        sem = Regex.Replace(sem, @"<[^>]+>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(sem), @"\s+", " ").Trim();
    }

    [GeneratedRegex(@"<input[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputRegex();
}

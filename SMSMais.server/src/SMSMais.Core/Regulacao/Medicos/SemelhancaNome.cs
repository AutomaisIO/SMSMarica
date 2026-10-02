using System.Globalization;
using System.Text;

namespace SMSMais.Core.Regulacao.Medicos;

/// <summary>
/// "Este médico já existe com outro nome?" — a comparação que pega o cadastro abreviado do SER.
///
/// <para><b>Por que existe:</b> o cadastro de médicos do SER é cheio de nome abreviado e sobrenome a
/// mais. Em 01/10/2026 uma unidade ia cadastrar "LAURA BEATRIZ ANDRADE RODRIGUES" porque não a
/// achou — ela estava lá como "LAURA BEATRIZ A. RODRIGUES VILELA". No SER não há editar nem apagar:
/// a duplicata seria para sempre.</para>
///
/// <para><b>A regra</b> (testada contra os 928 médicos reais do combo do SER): sem acento e sem
/// partículas (DE, DA, DOS…), palavra a palavra e EM ORDEM; uma palavra casa com outra se for igual,
/// se uma for a inicial da outra ("A." = ANDRADE), se for abreviação por prefixo ("ROD" = RODRIGUES)
/// ou se tiver um erro de digitação (palavras de 5+ letras). O primeiro nome tem de bater por extenso
/// e ao menos duas palavras inteiras têm de coincidir — só iniciais casariam meio cadastro. Sobrenome
/// a mais de um dos lados não atrapalha.</para>
///
/// <para>Ela <b>sugere</b>, nunca decide: "MARIA SILVA" casa com várias Marias Silva, e quem escolhe é
/// a pessoa.</para>
/// </summary>
public static class SemelhancaNome
{
    private static readonly HashSet<string> Particulas = ["DE", "DA", "DO", "DAS", "DOS", "E", "DR", "DRA"];

    public static IReadOnlyList<string> Palavras(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return [];
        var sb = new StringBuilder(nome.Length);
        foreach (var ch in nome.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetter(ch) ? char.ToUpperInvariant(ch) : ' ');
        }
        return [.. sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => !Particulas.Contains(p))];
    }

    /// <summary>De 0 a 1: a fração das palavras do nome mais curto que casou. 0 = não é parecido.</summary>
    public static double Pontuacao(string digitado, string candidato)
    {
        var a = Palavras(digitado);
        var b = Palavras(candidato);
        if (a.Count == 0 || b.Count == 0) return 0;

        // O primeiro nome é o que menos se abrevia: tem de bater por extenso.
        if (a[0].Length == 1 || b[0].Length == 1 || !Casa(a[0], b[0])) return 0;

        var j = 0;
        var casadas = 0;
        var inteiras = 0;
        foreach (var x in a)
        {
            for (var k = j; k < b.Count; k++)
            {
                if (!Casa(x, b[k])) continue;
                casadas++;
                if (x.Length > 1 && b[k].Length > 1) inteiras++;
                j = k + 1;
                break;
            }
        }

        return inteiras >= 2 ? (double)casadas / Math.Min(a.Count, b.Count) : 0;
    }

    private static bool Casa(string x, string y) =>
        x == y
        || (x.Length == 1 && y[0] == x[0])
        || (y.Length == 1 && x[0] == y[0])
        || (Math.Min(x.Length, y.Length) >= 3 && (x.StartsWith(y, StringComparison.Ordinal) || y.StartsWith(x, StringComparison.Ordinal)))
        || (Math.Min(x.Length, y.Length) >= 5 && UmErro(x, y));

    /// <summary>Distância de edição ≤ 1 (uma letra trocada, sobrando ou faltando).</summary>
    private static bool UmErro(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1) return false;
        int i = 0, j = 0, erros = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] == b[j]) { i++; j++; continue; }
            if (++erros > 1) return false;
            if (a.Length > b.Length) i++;
            else if (b.Length > a.Length) j++;
            else { i++; j++; }
        }
        return erros + (a.Length - i) + (b.Length - j) <= 1;
    }

    /// <summary>Só os dígitos do documento — "52140159-2", "52.140159-2" e "521401592" são o mesmo CRM.</summary>
    public static string Digitos(string? documento) =>
        new([.. (documento ?? string.Empty).Where(char.IsDigit)]);
}

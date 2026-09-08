using System.Globalization;
using System.Text;

namespace SMSMais.Core.Regulacao.Regras;

/// <summary>
/// Normalização dos rótulos de procedimento, usada para casar o que os sistemas escrevem de
/// jeitos diferentes.
///
/// <para>O mesmo procedimento aparece como <c>CONSULTA EM OFTALMOLOGIA</c> no catálogo e
/// <c>Consulta  em Oftalmologia</c> na solicitação; o manual escreve com acento onde o catálogo
/// escreve sem. Tira acento, tira pontuação, colapsa espaço e sobe para caixa alta — o suficiente
/// para casar sem casar demais.</para>
///
/// <para>Está aqui, e não dentro de um dos serviços, porque o importador de regras e o ranking de
/// demanda precisam da <b>mesma</b> chave: se divergirem, o rótulo casa num lugar e não no outro,
/// e a regra some do procedimento em que ela aparece.</para>
/// </summary>
public static class ChaveRotulo
{
    public static string Normalizar(string? rotulo)
    {
        if (string.IsNullOrWhiteSpace(rotulo)) return string.Empty;

        var sb = new StringBuilder(rotulo.Length);
        foreach (var ch in rotulo.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch) || ch == ' ') sb.Append(ch);
        }

        var normalizado = sb.ToString().ToUpperInvariant().Trim();

        // Espaço duplo existe nos rótulos do SER; sem colapsar, o mesmo nome não casa consigo.
        while (normalizado.Contains("  ", StringComparison.Ordinal))
        {
            normalizado = normalizado.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalizado;
    }
}

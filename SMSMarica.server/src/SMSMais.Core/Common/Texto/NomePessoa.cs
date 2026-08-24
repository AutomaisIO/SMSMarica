using System.Globalization;

namespace SMSMais.Core.Common.Texto;

/// <summary>
/// Nomes de pessoa para EXIBIÇÃO. As bases de origem (SISREG, CADWEB, importações) gravam
/// tudo em CAIXA ALTA; jogado cru numa mensagem de WhatsApp isso lê como grito. Aqui só
/// mudamos a apresentação — o nome oficial no FHIR continua como veio.
/// </summary>
public static class NomePessoa
{
    /// <summary>Partículas que ficam em minúscula no meio do nome (nunca na primeira palavra).</summary>
    private static readonly HashSet<string> Particulas = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "da", "do", "das", "dos", "e", "di", "du", "del", "della", "van", "von", "y",
    };

    /// <summary>
    /// "MARIA DAS DORES DA SILVA" → "Maria das Dores da Silva". Nome já capitalizado passa
    /// intacto; hífen e apóstrofo capitalizam o pedaço seguinte (Jean-Pierre, D'Ávila).
    /// </summary>
    public static string Capitalizar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return nome ?? "";

        var palavras = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var saida = palavras.Select((p, i) =>
            i > 0 && Particulas.Contains(p) ? p.ToLowerInvariant() : CapitalizarPalavra(p));

        return string.Join(' ', saida);
    }

    /// <summary>Primeiro nome, já capitalizado — é como o operador/paciente aparece na mensagem.</summary>
    public static string PrimeiroNome(string? nome)
    {
        var primeiro = (nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return primeiro is null ? (nome ?? "") : CapitalizarPalavra(primeiro);
    }

    private static string CapitalizarPalavra(string palavra)
    {
        var texto = palavra.ToLowerInvariant();
        var chars = texto.ToCharArray();
        var inicioDeParte = true;

        for (var i = 0; i < chars.Length; i++)
        {
            if (inicioDeParte && char.IsLetter(chars[i]))
            {
                chars[i] = char.ToUpper(chars[i], CultureInfo.GetCultureInfo("pt-BR"));
                inicioDeParte = false;
            }
            else if (chars[i] is '-' or '\'' or '’')
            {
                inicioDeParte = true;
            }
        }

        return new string(chars);
    }
}

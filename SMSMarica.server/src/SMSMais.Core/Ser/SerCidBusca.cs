using System.Globalization;
using System.Text;

namespace SMSMais.Core.Ser;

/// <summary>
/// Como um CID é procurado no catálogo espelhado.
///
/// <para><b>Existe para ser um só.</b> A coluna <c>busca</c> é gravada na cópia e comparada na
/// consulta; se os dois lados normalizassem de jeitos diferentes, a tela simplesmente não acharia
/// nada — sem erro nenhum, que é como as coisas quebram neste sistema.</para>
///
/// <para>O SER casa "contém" e o dado dele é irregular: "Diarréia" tem acento, "Hipertensao" não.
/// Comparar sem acento é o que faz "hipertensão" achar "Hipertensao". Isso não amplia o que é
/// oferecido — o conjunto continua sendo exatamente o que o SER devolveu.</para>
/// </summary>
public static class SerCidBusca
{
    /// <summary>O texto de busca de um CID: código e descrição, sem acento, em minúsculas.</summary>
    public static string De(string codigo, string descricao) =>
        Normalizar($"{codigo} {descricao}");

    /// <summary>Mesma régua para o termo digitado.</summary>
    public static string Normalizar(string texto)
    {
        var decomposto = (texto ?? string.Empty).Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);

        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}

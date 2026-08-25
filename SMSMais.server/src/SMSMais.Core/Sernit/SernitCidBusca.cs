using System.Globalization;
using System.Text;

namespace SMSMais.Core.Sernit;

/// <summary>
/// Como um CID é procurado no catálogo espelhado do SERNIT. Uma régua só: a coluna <c>busca</c> é
/// gravada na cópia e comparada na consulta — normalizar diferente nos dois lados faria a tela não
/// achar nada. Sem acento, minúsculas (o dado do SERNIT é irregular: "Diarréia" com acento,
/// "Hipertensao" sem). Não amplia o conjunto — só torna encontrável o que já está lá.
/// </summary>
public static class SernitCidBusca
{
    public static string De(string codigo, string descricao) => Normalizar($"{codigo} {descricao}");

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

using System.Globalization;
using System.Text;

namespace SMSMais.Core.Integracoes.SisregWeb.Varredura.Sigtap;

/// <summary>
/// Heurística que sugere qual procedimento SIGTAP corresponde a um procedimento do SISREG,
/// comparando os nomes. Pura e sem I/O — o catálogo entra como lista.
///
/// <para><b>A regra que não se negocia:</b> só igualdade exata (depois de normalizar) vira
/// auto-confirmação. Semelhança alta é <i>sugestão</i>, e sugestão espera o operador. SIGTAP
/// errado não dá erro em lugar nenhum: dá worklist errada, exame errado no PACS e laudo no
/// lugar errado — o tipo de defeito que só aparece semanas depois, no paciente.</para>
/// </summary>
public static class SugestaoSigtap
{
    /// <summary>Abaixo disso não vale nem sugerir — o operador perde mais tempo conferindo lixo.</summary>
    public const double ScoreMinimo = 0.5;

    /// <summary>Um candidato do catálogo SIGTAP.</summary>
    public sealed record Candidato(Guid Id, string Codigo, string Nome);

    /// <summary>Sugestão para um procedimento do SISREG. <see cref="Exata"/> autoriza confirmar sozinho.</summary>
    public sealed record Resultado(Guid ProcedimentoSigtapId, string Codigo, double Score, bool Exata);

    /// <summary>
    /// Melhor candidato para <paramref name="nomeSisreg"/>, ou null se nada passou do mínimo.
    /// </summary>
    public static Resultado? Sugerir(string nomeSisreg, IReadOnlyList<Candidato> catalogo)
    {
        var alvo = Normalizar(nomeSisreg);
        if (alvo.Length == 0 || catalogo.Count == 0) return null;

        var tokensAlvo = Tokens(alvo);
        Resultado? melhor = null;

        foreach (var candidato in catalogo)
        {
            var nome = Normalizar(candidato.Nome);

            if (string.Equals(nome, alvo, StringComparison.Ordinal))
                return new Resultado(candidato.Id, candidato.Codigo, 1.0, Exata: true);

            var score = Jaccard(tokensAlvo, Tokens(nome));
            if (score >= ScoreMinimo && (melhor is null || score > melhor.Score))
                melhor = new Resultado(candidato.Id, candidato.Codigo, score, Exata: false);
        }

        return melhor;
    }

    /// <summary>
    /// Maiúsculas, sem acento, sem pontuação, espaços colapsados, e sem o prefixo <c>GRUPO -</c>
    /// que o SISREG usa nos procedimentos agregadores (o nome depois dele é o que importa).
    /// </summary>
    public static string Normalizar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;

        var texto = valor.Trim().ToUpperInvariant();

        // "GRUPO - MAMOGRAFIA" e "GRUPO-MAMOGRAFIA" → "MAMOGRAFIA"
        const string prefixoGrupo = "GRUPO";
        if (texto.StartsWith(prefixoGrupo, StringComparison.Ordinal))
        {
            var resto = texto[prefixoGrupo.Length..].TrimStart();
            if (resto.StartsWith('-')) texto = resto[1..].TrimStart();
        }

        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var construtor = new StringBuilder(decomposto.Length);
        var espacoPendente = false;

        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(caractere))
            {
                if (espacoPendente && construtor.Length > 0) construtor.Append(' ');
                espacoPendente = false;
                construtor.Append(caractere);
            }
            else
            {
                // Pontuação e espaço viram o mesmo separador — "1a. VEZ" e "1a VEZ" batem.
                espacoPendente = true;
            }
        }

        return construtor.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Tokens com 3+ caracteres — "DE", "EM", "DO" não distinguem procedimento nenhum.</summary>
    private static HashSet<string> Tokens(string normalizado) =>
        [.. normalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 3)];

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var intersecao = a.Count(b.Contains);
        var uniao = a.Count + b.Count - intersecao;
        return uniao == 0 ? 0 : (double)intersecao / uniao;
    }
}

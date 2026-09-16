using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace SMSMais.Core.Integracoes.KlinikosWeb.Cid;

/// <summary>
/// De-para de CID por TEXTO. Os relatórios e telas do Klinikos web trazem a DESCRIÇÃO do
/// diagnóstico (ex.: "NASOFARINGITE AGUDA [RESFRIADO COMUM]"), às vezes SEM o código — enquanto o
/// conector SQL traz código+nome de <c>TB_CID</c>. Para o teste de paridade bater (mesmo
/// <c>Condition.code</c>) é preciso recuperar o código a partir do texto.
///
/// <para>Regra: normaliza (maiúsculas, sem acento, sem o sufixo entre colchetes, espaço único) e
/// casa contra o catálogo carregado (descrição → código). <b>Sem correspondência confiável NÃO
/// inventa código</b> — devolve só o texto (<see cref="CidResolvido.Mapeado"/> = false) e o
/// chamador registra a exceção para tratamento manual.</para>
/// </summary>
public interface ICidDeParaService
{
    /// <summary>Carrega/atualiza o catálogo (descrição→código) — tipicamente semeado de <c>TB_CID</c>.</summary>
    void CarregarCatalogo(IEnumerable<(string Codigo, string Descricao)> catalogo);

    /// <summary>Quantas descrições o catálogo conhece.</summary>
    int CatalogoCount { get; }

    /// <summary>Resolve o texto do CID em código quando possível; sempre preserva o texto original.</summary>
    CidResolvido Resolver(string? cidTexto);
}

public sealed class CidDeParaService : ICidDeParaService
{
    private ConcurrentDictionary<string, string> _porDescricao = new(StringComparer.Ordinal);

    public int CatalogoCount => _porDescricao.Count;

    public void CarregarCatalogo(IEnumerable<(string Codigo, string Descricao)> catalogo)
    {
        var mapa = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (codigo, descricao) in catalogo)
        {
            var chave = NormalizarDescricao(descricao);
            if (chave.Length == 0 || string.IsNullOrWhiteSpace(codigo)) continue;
            // Primeira descrição vence (o catálogo pode ter sinônimos); não sobrescreve.
            mapa.TryAdd(chave, codigo.Trim().ToUpperInvariant());
        }
        _porDescricao = mapa;
    }

    public CidResolvido Resolver(string? cidTexto)
    {
        var texto = (cidTexto ?? string.Empty).Trim();
        if (texto.Length == 0) return new CidResolvido(null, string.Empty, Mapeado: false);

        // 1) O texto já traz um código explícito? (ex.: "J00 - NASOFARINGITE", "J00 NASOFARINGITE").
        if (CodigoExplicito(texto) is { } explicito)
        {
            return new CidResolvido(explicito, texto, Mapeado: true);
        }

        // 2) Casa a descrição normalizada contra o catálogo.
        var chave = NormalizarDescricao(texto);
        return _porDescricao.TryGetValue(chave, out var codigo)
            ? new CidResolvido(codigo, texto, Mapeado: true)
            : new CidResolvido(null, texto, Mapeado: false);
    }

    /// <summary>
    /// Código CID-10 explícito no começo do texto: 1 letra + 2 dígitos, com dígito extra e ponto
    /// opcionais (A00, J00, M545, M54.5), seguido de fim, espaço, hífen ou dois-pontos. Fora disso
    /// não é código — é descrição.
    /// </summary>
    private static string? CodigoExplicito(string texto)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            texto, @"^([A-Za-z]\d{2}\.?\d?)(?=$|[\s\-:])");
        return m.Success ? m.Groups[1].Value.ToUpperInvariant().Replace(".", string.Empty, StringComparison.Ordinal) : null;
    }

    /// <summary>
    /// Normaliza a descrição para casar: maiúsculas, sem acento, remove o sufixo entre colchetes
    /// ("[RESFRIADO COMUM]"), colapsa espaço e tira pontuação. É a mesma ideia da chave do
    /// catálogo no conector SQL, mas do lado da DESCRIÇÃO, não do código.
    /// </summary>
    internal static string NormalizarDescricao(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        // Remove sufixo entre colchetes/parênteses (sinônimo popular).
        var semSufixo = System.Text.RegularExpressions.Regex.Replace(texto, @"[\[\(].*?[\]\)]", " ");

        var semAcento = new string(
            semSufixo.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());

        var sb = new StringBuilder(semAcento.Length);
        var espaco = false;
        foreach (var c in semAcento)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (espaco && sb.Length > 0) sb.Append(' ');
                espaco = false;
                sb.Append(char.ToUpperInvariant(c));
            }
            else
            {
                espaco = true;
            }
        }
        return sb.ToString();
    }
}

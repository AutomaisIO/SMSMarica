using System.Text.RegularExpressions;

namespace Automais.Pabx.Api.Asterisk;

public sealed record SecaoSip(string Nome, Dictionary<string, string> Campos);

/// <summary>
/// Parser somente-leitura do sip_custom.conf legado (FalarMais) para a adoção dos
/// ramais pré-existentes. Nunca escreve nesse arquivo.
/// </summary>
public static partial class SipCustomParser
{
    [GeneratedRegex(@"^\[(?<nome>[^\]]+)\]\s*$")]
    private static partial Regex SecaoRegex();

    public static IReadOnlyList<SecaoSip> Parse(string conteudo)
    {
        var secoes = new List<SecaoSip>();
        SecaoSip? atual = null;

        foreach (var linhaBruta in conteudo.Split('\n'))
        {
            var linha = linhaBruta.Trim();
            if (linha.Length == 0 || linha.StartsWith(';'))
                continue;

            var m = SecaoRegex().Match(linha);
            if (m.Success)
            {
                atual = new SecaoSip(m.Groups["nome"].Value.Trim(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                secoes.Add(atual);
                continue;
            }

            var separador = linha.IndexOf('=');
            if (atual is not null && separador > 0)
            {
                var chave = linha[..separador].Trim();
                var valor = linha[(separador + 1)..].Trim();
                // comentário no fim da linha ("secret=x ; obs")
                var comentario = valor.IndexOf(';');
                if (comentario >= 0)
                    valor = valor[..comentario].Trim();
                atual.Campos[chave] = valor;
            }
        }

        return secoes;
    }

    /// <summary>Seções cujo nome é numérico = ramais (troncos/templates têm nomes com letras).</summary>
    public static IReadOnlyList<SecaoSip> SomenteRamais(IReadOnlyList<SecaoSip> secoes) =>
        [.. secoes.Where(s => s.Nome.Length > 0 && s.Nome.All(char.IsAsciiDigit))];
}

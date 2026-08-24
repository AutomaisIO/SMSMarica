using System.Text;

namespace SMSMais.Core.Inteligencia.Conhecimento;

/// <summary>
/// Quebra um markdown em pedaços para embedding: separa por seções (linhas iniciadas por '#')
/// e, dentro de cada seção, agrupa parágrafos até um limite de caracteres. Compartilhado entre
/// a sincronização de docs do repo e a gestão de docs pela tela.
/// </summary>
public static class ChunkificadorMarkdown
{
    public static List<string> Chunkificar(string conteudo, int maxChars = 1500)
    {
        var resultado = new List<string>();

        foreach (var secao in QuebrarPorSecao(conteudo))
        {
            var paragrafos = secao.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);
            var atual = new StringBuilder();

            foreach (var paragrafo in paragrafos)
            {
                var trecho = paragrafo.Trim();
                if (trecho.Length == 0)
                {
                    continue;
                }

                if (atual.Length > 0 && atual.Length + trecho.Length > maxChars)
                {
                    resultado.Add(atual.ToString().Trim());
                    atual.Clear();
                }

                if (atual.Length > 0)
                {
                    atual.Append("\n\n");
                }

                atual.Append(trecho);
            }

            if (atual.Length > 0)
            {
                resultado.Add(atual.ToString().Trim());
            }
        }

        return resultado;
    }

    private static List<string> QuebrarPorSecao(string conteudo)
    {
        var linhas = conteudo.Split('\n');
        var secoes = new List<string>();
        var atual = new StringBuilder();

        foreach (var linha in linhas)
        {
            if (linha.StartsWith('#') && atual.Length > 0)
            {
                secoes.Add(atual.ToString());
                atual.Clear();
            }

            atual.Append(linha).Append('\n');
        }

        if (atual.Length > 0)
        {
            secoes.Add(atual.ToString());
        }

        return secoes;
    }
}

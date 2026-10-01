using System.Text.RegularExpressions;

namespace SMSMais.Core.Extensao.Distribuicao;

/// <summary>
/// Versões <c>1.2.3</c> — as do manifest do Chrome e as do atualizador. A comparação é a mesma
/// que o atualizador faz no computador (número a número; parte ausente vale zero): se os dois
/// lados discordassem sobre qual é "a mais nova", o computador ignoraria o que o painel anuncia.
/// </summary>
public static partial class VersaoPublicada
{
    [GeneratedRegex(@"^\d{1,9}(\.\d{1,9}){0,3}$")]
    private static partial Regex Formato();

    public static bool Valida(string? versao) => versao is not null && Formato().IsMatch(versao);

    public static int Comparar(string a, string b)
    {
        var x = Partes(a);
        var y = Partes(b);
        for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            var comparacao = (i < x.Length ? x[i] : 0).CompareTo(i < y.Length ? y[i] : 0);
            if (comparacao != 0) return comparacao;
        }
        return 0;
    }

    public static bool MaisNova(string a, string b) => Comparar(a, b) > 0;

    private static long[] Partes(string versao) =>
        versao.Trim().Split('.').Select(p => long.TryParse(p, out var n) ? n : 0).ToArray();
}

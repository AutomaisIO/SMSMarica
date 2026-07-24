using System.Globalization;
using System.Text.RegularExpressions;
using SMSMarica.Core.Common.Excecoes;

namespace SMSMarica.Core.Indicadores;

/// <summary>
/// Substitui os parâmetros nomeados do SQL do indicador (<c>:ini</c>, <c>:fim</c>,
/// <c>:hospital</c>) pelos literais Oracle correspondentes.
///
/// Por que substituição e não bind: a <c>IFonteDados</c> executa uma string de consulta e não
/// recebe parâmetros. A substituição só é segura porque os valores são <b>tipados</b>
/// (<see cref="DateOnly"/> e <see cref="int"/>) e formatados aqui — nunca texto vindo do
/// usuário. Qualquer parâmetro desconhecido faz a execução falhar em vez de virar SQL solto,
/// e o resultado ainda passa pelo guard read-only antes de sair.
/// </summary>
public static partial class ParametrosIndicador
{
    [GeneratedRegex(@":(?<nome>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex Nomeados();

    private static readonly string[] Conhecidos = ["ini", "fim", "hospital"];

    public static string Aplicar(string sql, int hospital, DateOnly inicio, DateOnly fim)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        if (fim < inicio)
        {
            throw new ValidacaoException("indicador.periodo", "A data final é anterior à inicial.");
        }

        var desconhecidos = Nomeados()
            .Matches(sql)
            .Select(m => m.Groups["nome"].Value)
            .Where(n => !Conhecidos.Contains(n, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (desconhecidos.Length > 0)
        {
            throw new ValidacaoException(
                "indicador.sql",
                $"Parâmetro não reconhecido: :{string.Join(", :", desconhecidos)}. " +
                "Disponíveis: :ini, :fim, :hospital.");
        }

        // :fim é exclusivo no SQL (dt < :fim) — somamos 1 dia para o filtro cobrir o dia final inteiro.
        return Nomeados().Replace(sql, m => m.Groups["nome"].Value.ToLowerInvariant() switch
        {
            "ini" => Data(inicio),
            "fim" => Data(fim.AddDays(1)),
            "hospital" => hospital.ToString(CultureInfo.InvariantCulture),
            _ => m.Value,
        });
    }

    private static string Data(DateOnly d) =>
        $"TO_DATE('{d:yyyy-MM-dd}','YYYY-MM-DD')";
}

using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace SMSMarica.Core.Integracoes.SisregWeb;

/// <summary>
/// Endereço/telefone do CADSUS. <b>Mapeado mas NÃO usado no auto-preenchimento</b> —
/// na prática esses dados vêm quase sempre desatualizados no SISREG. Ficam aqui para
/// quando/se decidirmos aproveitá-los (ex.: sugestão, conferência).
/// </summary>
public sealed record CadsusEndereco(
    string? TipoLogradouro, string? Logradouro, string? Complemento, string? Numero,
    string? Bairro, string? Cep, string? Pais, string? MunicipioResidencia);

/// <summary>Registro completo extraído da ficha CADSUS (<c>cadweb50</c>).</summary>
public sealed record CadsusRegistro(
    string Cns, string Cpf, string Nome, string? Sexo, DateOnly? DataNascimento,
    string? NomeMae, string? NomePai, string? Raca, string? Nacionalidade, string? MunicipioNascimento,
    CadsusEndereco Endereco, string? Telefone);

/// <summary>
/// Parser da ficha "CONSULTA AO CADASTRO DE PACIENTES SUS" (SISREG III <c>cadweb50</c>).
/// A ficha é uma tabela onde os rótulos (terminados em ':') ficam numa linha e os valores
/// na linha seguinte, alinhados por coluna. Extraímos por par rótulo→valor.
/// </summary>
public static partial class CadsusHtmlParser
{
    /// <summary>Interpreta o HTML da ficha. Retorna null se não houver paciente (ou for outra tela).</summary>
    public static CadsusRegistro? Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var doc = new HtmlParser().ParseDocument(html);
        var linhas = doc.QuerySelectorAll("tr").ToArray();

        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < linhas.Length; i++)
        {
            var rotulos = Celulas(linhas[i]);
            var naoVazios = rotulos.Where(c => c.Length > 0).ToList();
            var ehLinhaRotulo = naoVazios.Count > 0 && naoVazios.All(c => c.EndsWith(":", StringComparison.Ordinal));
            if (!ehLinhaRotulo || i + 1 >= linhas.Length) continue;

            var valores = Celulas(linhas[i + 1]);
            for (var k = 0; k < rotulos.Count; k++)
            {
                var chave = rotulos[k].TrimEnd(':').Trim();
                if (chave.Length == 0 || mapa.ContainsKey(chave)) continue;
                mapa[chave] = k < valores.Count ? valores[k] : string.Empty;
            }
        }

        var cns = Digitos(Valor(mapa, "CNS"));
        var cpf = Digitos(Valor(mapa, "CPF"));
        var nome = Valor(mapa, "Nome").Trim();

        // Sem qualquer identificador/nome → não é uma ficha de paciente (tela de login, "não encontrado", etc.).
        if (nome.Length == 0 && cns.Length == 0 && cpf.Length == 0) return null;

        var endereco = new CadsusEndereco(
            LimparNulo(Valor(mapa, "Tipo Logradouro")),
            LimparNulo(Valor(mapa, "Logradouro")),
            LimparNulo(Valor(mapa, "Complemento")),
            LimparNulo(Valor(mapa, "Número")),
            LimparNulo(Valor(mapa, "Bairro")),
            Digitos(Valor(mapa, "CEP")) is { Length: 8 } cep ? cep : null,
            LimparNulo(Valor(mapa, "País de Residência")),
            LimparNulo(Valor(mapa, "Município de Residência")));

        return new CadsusRegistro(
            Cns: cns,
            Cpf: cpf,
            Nome: nome,
            Sexo: NormalizarSexo(Valor(mapa, "Sexo")),
            DataNascimento: ExtrairData(Valor(mapa, "Data de Nascimento")),
            NomeMae: LimparNulo(Valor(mapa, "Nome da Mãe")),
            NomePai: LimparNulo(Valor(mapa, "Nome do Pai")),
            Raca: LimparNulo(Valor(mapa, "Raça")),
            Nacionalidade: LimparNulo(Valor(mapa, "Nacionalidade")),
            MunicipioNascimento: LimparNulo(Valor(mapa, "Município de Nascimento")),
            Endereco: endereco,
            Telefone: LimparNulo(Valor(mapa, "Telefone(s)")));
    }

    /// <summary>Detecta a tela de login propriamente dita.</summary>
    public static bool EhTelaLogin(string html) =>
        html.Contains("name=\"senha_256\"", StringComparison.OrdinalIgnoreCase)
        && html.Contains("name=\"formLogin\"", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sessão inválida: ou voltou a tela de login, ou a página "Erro de Sistema — este
    /// operador efetuou logon em outra estação / sua sessão foi finalizada" (sessão única
    /// derrubada por uso concorrente do operador). Em ambos os casos: relogar e repetir.
    /// </summary>
    public static bool SessaoInvalida(string html) =>
        EhTelaLogin(html)
        || html.Contains("logon em outra", StringComparison.OrdinalIgnoreCase)
        || html.Contains("foi finalizada", StringComparison.OrdinalIgnoreCase);

    private static List<string> Celulas(AngleSharp.Dom.IElement tr) =>
        [.. tr.QuerySelectorAll("td, th").Select(c => EspacosRegex().Replace(c.TextContent, " ").Trim())];

    private static string Valor(IReadOnlyDictionary<string, string> mapa, string chave) =>
        mapa.TryGetValue(chave, out var v) ? v : string.Empty;

    private static string Digitos(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string([.. s.Where(char.IsDigit)]);

    private static string? LimparNulo(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        if (t.Length == 0) return null;
        var upper = t.ToUpperInvariant();
        // CADSUS usa "SEM INFORMAÇÃO"/"---" como ausência.
        return upper is "SEM INFORMACAO" or "SEM INFORMAÇÃO" || t.Trim('-', ' ', '.').Length == 0 ? null : t;
    }

    private static string? NormalizarSexo(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;
        return bruto.Trim().ToUpperInvariant() switch
        {
            "M" or "MASCULINO" => "Masculino",
            "F" or "FEMININO" => "Feminino",
            _ => null,
        };
    }

    private static DateOnly? ExtrairData(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;
        var m = DataRegex().Match(bruto);
        return m.Success && DateOnly.TryParseExact(m.Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosRegex();

    [GeneratedRegex(@"\d{2}/\d{2}/\d{4}")]
    private static partial Regex DataRegex();
}

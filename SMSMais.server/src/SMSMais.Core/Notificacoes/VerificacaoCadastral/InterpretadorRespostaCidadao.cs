using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SMSMais.Core.Notificacoes.VerificacaoCadastral;

/// <summary>
/// Interpretação DETERMINÍSTICA das respostas do cidadão no fluxo de verificação cadastral —
/// nenhum LLM aqui. Cada método é tolerante ao "jeito que as pessoas escrevem no WhatsApp"
/// (pontuação de CPF, datas em vários formatos, mês por extenso, "sim/ss/isso"), mas
/// conservador: na dúvida devolve nulo/falso e o fluxo re-pergunta em vez de adivinhar.
/// </summary>
public static partial class InterpretadorRespostaCidadao
{
    // ---------- CPF ----------

    /// <summary>
    /// Extrai os dígitos de uma resposta de CPF ("0452", "045.288.227-33", "meu cpf é 0452…").
    /// Regras: remove pontuação/espaços; exige de 4 a 11 dígitos (mínimo 4 — regra do fluxo);
    /// rejeita textos com "/" (provável data) e respostas com mais texto que números.
    /// </summary>
    public static string? ExtrairDigitosCpf(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        if (t.Contains('/')) return null; // "03/1980" é data, não CPF

        var digitos = SoDigitos(t);
        if (digitos.Length is < 4 or > 11) return null;

        // A resposta deve ser essencialmente numérica: tolera rótulos comuns ("cpf", "meu cpf é",
        // "começa com") mas rejeita frases longas — nelas, números podem ser outra coisa.
        var semRotulos = Regex.Replace(Normalizar(t),
            @"\b(cpf|meu|minha|o|a|e|eh|sao|os|primeiros|digitos|numeros|comeca|com|inicio|do)\b", " ");
        var letrasRestantes = Regex.Replace(semRotulos, @"[\d\s\.\-:,;]", string.Empty).Length;
        return letrasRestantes > 3 ? null : digitos;
    }

    /// <summary>Prefixo do CPF confere? Compara os N dígitos informados (N≥4) com o começo do
    /// CPF cadastrado. CPF completo (11) também passa por aqui.</summary>
    public static bool CpfPrefixoConfere(string? cpfCadastrado, string? digitosInformados)
    {
        var cad = SoDigitos(cpfCadastrado ?? string.Empty);
        var inf = SoDigitos(digitosInformados ?? string.Empty);
        if (cad.Length != 11 || inf.Length is < 4 or > 11) return false;
        return cad.StartsWith(inf, StringComparison.Ordinal);
    }

    // ---------- Nascimento ----------

    /// <summary>
    /// Lê mês/ano (e dia, quando dado) de uma resposta de nascimento. Formatos aceitos:
    /// dd/mm/aaaa, dd/mm/aa, dd-mm-aaaa, mm/aaaa, mm/aa, "03 1980", "3 de março de 1980",
    /// "março de 1980", "mar/1980". Ano de 2 dígitos: resolve para 19xx/20xx pelo mais plausível
    /// como nascimento. Devolve nulo quando não entender.
    /// </summary>
    public static (int? Dia, int Mes, int Ano)? TentarLerNascimento(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = Normalizar(texto);

        // 1) Mês por extenso: "[dia de] março de 1980" / "mar 1980" / "marco/80"
        var m = RegexMesExtenso().Match(t);
        if (m.Success)
        {
            var mes = MesPorNome(m.Groups["mes"].Value);
            var ano = NormalizarAno(int.Parse(m.Groups["ano"].Value, CultureInfo.InvariantCulture));
            int? dia = m.Groups["dia"].Success ? int.Parse(m.Groups["dia"].Value, CultureInfo.InvariantCulture) : null;
            if (mes is >= 1 and <= 12 && AnoPlausivel(ano) && DiaPlausivel(dia))
                return (dia, mes, ano);
            return null;
        }

        // 2) Numérico com separadores (/ - . espaço): 2 ou 3 grupos.
        var grupos = Regex.Matches(t, @"\d{1,4}").Select(x => x.Value).ToList();
        if (grupos.Count is < 2 or > 3) return null;
        // Grupo colado tipo "031980"/"03081980"? — só quando for UM grupo; aqui exigimos separadores.

        if (grupos.Count == 2)
        {
            // mm/aaaa ou mm/aa
            var mes = int.Parse(grupos[0], CultureInfo.InvariantCulture);
            var ano = NormalizarAno(int.Parse(grupos[1], CultureInfo.InvariantCulture));
            if (mes is >= 1 and <= 12 && grupos[0].Length <= 2 && AnoPlausivel(ano))
                return (null, mes, ano);
            return null;
        }

        // dd/mm/aaaa ou dd/mm/aa
        var d = int.Parse(grupos[0], CultureInfo.InvariantCulture);
        var me = int.Parse(grupos[1], CultureInfo.InvariantCulture);
        var an = NormalizarAno(int.Parse(grupos[2], CultureInfo.InvariantCulture));
        if (DiaPlausivel(d) && me is >= 1 and <= 12 && grupos[0].Length <= 2 && grupos[1].Length <= 2 && AnoPlausivel(an))
            return (d, me, an);
        return null;
    }

    /// <summary>
    /// A resposta de nascimento confere com a data cadastrada? Mês e ano são obrigatórios;
    /// quando a pessoa informa também o DIA, ele precisa bater (informação a mais errada é
    /// sinal de pessoa errada, não de tolerância).
    /// </summary>
    public static bool NascimentoConfere(DateOnly? nascimentoCadastrado, (int? Dia, int Mes, int Ano)? resposta)
    {
        if (nascimentoCadastrado is not { } n || resposta is not { } r) return false;
        if (n.Month != r.Mes || n.Year != r.Ano) return false;
        return r.Dia is null || r.Dia == n.Day;
    }

    // ---------- Sim / Não / Nome ----------

    /// <summary>"Sim" e variações de WhatsApp (s, ss, sim sou, isso, sou eu, confirmo, correto, exato, positivo).</summary>
    public static bool EhSim(string? texto)
    {
        var t = Normalizar(texto ?? string.Empty).Trim('!', '.', ' ');
        return t is "sim" or "s" or "ss" or "sim sou" or "sou" or "sou eu" or "sou eu sim" or "isso"
            or "isso mesmo" or "confirmo" or "confirmado" or "correto" or "certo" or "exato"
            or "positivo" or "eu mesmo" or "eu mesma" or "sim sou eu" or "sim eu" or "ok sim";
    }

    /// <summary>"Não" e variações (n, nao, não sou, errado, negativo, não sou eu).</summary>
    public static bool EhNao(string? texto)
    {
        var t = Normalizar(texto ?? string.Empty).Trim('!', '.', ' ');
        return t is "nao" or "n" or "nn" or "nao sou" or "nao sou eu" or "nao e" or "nao eh"
            or "errado" or "negativo" or "nao senhor" or "nao senhora";
    }

    /// <summary>
    /// Em vez de "sim", a pessoa pode DIGITAR o próprio nome. Considera confirmação quando a
    /// resposta compartilha ao menos dois nomes (primeiro + algum outro) com o nome cadastrado.
    /// </summary>
    public static bool NomeConfere(string? nomeCadastrado, string? resposta)
    {
        var cad = Normalizar(nomeCadastrado ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var res = Normalizar(resposta ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (cad.Length == 0 || res.Length < 2) return false;
        var emComum = res.Count(r => r.Length >= 3 && cad.Contains(r));
        return emComum >= 2 && cad.Length > 0 && res[0] == cad[0];
    }

    // ---------- Atendente ----------

    /// <summary>Pedido de atendente humano, em qualquer etapa ("prefiro falar com atendente",
    /// "quero falar com uma pessoa", "atendente", "falar com alguém", "quero um humano").</summary>
    public static bool PedeAtendente(string? texto)
    {
        var t = Normalizar(texto ?? string.Empty);
        if (t.Length == 0) return false;
        if (t.Contains("atendente") || t.Contains("atendimento humano")) return true;
        var pedeFalar = t.Contains("falar com") || t.Contains("quero falar") || t.Contains("preciso falar");
        return pedeFalar && (t.Contains("alguem") || t.Contains("pessoa") || t.Contains("humano")
            || t.Contains("gente") || t.Contains("moca") || t.Contains("moco"));
    }

    // ---------- utilitários ----------

    private static string SoDigitos(string s) => new([.. s.Where(char.IsAsciiDigit)]);

    /// <summary>minúsculas + sem acentos (para comparações tolerantes).</summary>
    public static string Normalizar(string s)
    {
        var lower = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(lower.Length);
        foreach (var c in lower)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
    }

    /// <summary>Ano de 2 dígitos → nascimento plausível: 30..99 → 19xx; 00..29 → 20xx.</summary>
    private static int NormalizarAno(int ano) => ano switch
    {
        >= 100 => ano,
        >= 30 => 1900 + ano,
        _ => 2000 + ano,
    };

    private static bool AnoPlausivel(int ano) => ano is >= 1900 and <= 2026;
    private static bool DiaPlausivel(int? dia) => dia is null or (>= 1 and <= 31);

    private static int MesPorNome(string nome) => Normalizar(nome) switch
    {
        "janeiro" or "jan" => 1,
        "fevereiro" or "fev" => 2,
        "marco" or "mar" => 3,
        "abril" or "abr" => 4,
        "maio" or "mai" => 5,
        "junho" or "jun" => 6,
        "julho" or "jul" => 7,
        "agosto" or "ago" => 8,
        "setembro" or "set" => 9,
        "outubro" or "out" => 10,
        "novembro" or "nov" => 11,
        "dezembro" or "dez" => 12,
        _ => 0,
    };

    [GeneratedRegex(
        @"(?:(?<dia>\d{1,2})\s*(?:de\s+)?)?(?<mes>janeiro|fevereiro|marco|março|abril|maio|junho|julho|agosto|setembro|outubro|novembro|dezembro|jan|fev|mar|abr|mai|jun|jul|ago|set|out|nov|dez)\W*(?:de\s+)?(?<ano>\d{2,4})",
        RegexOptions.IgnoreCase)]
    private static partial Regex RegexMesExtenso();
}

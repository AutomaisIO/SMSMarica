using System.Globalization;
using System.Text;

namespace SMSMais.Core.Integracoes.Cadastro;

/// <summary>
/// A pessoa que chegou de fora (pedido do SER, linha do SISREG, cadastro do e-SUS) é a MESMA da
/// ficha que a chave (CPF/CNS) achou na nossa base? Responde só a pergunta forte: <b>"são duas
/// pessoas?"</b> — e só diz que sim quando o primeiro nome E o nascimento discordam.
///
/// <para><b>Por que existe.</b> Caso de 21/10/2026: a UPA cadastrou uma
/// criança com o CPF da mãe. Em 10/08 a conciliação do SER achou essa ficha pelo CPF
/// num pedido da MÃE e acrescentou nela o CNS da mãe — a trilha registrou o nome da mãe ao
/// lado do da filha e seguiu. A partir daí a importação do SISREG, que resolve pelo CNS, mandou os
/// pedidos da mãe para a ficha da filha; e a rotina do e-SUS, que
/// busca pelo CPF da ficha, trocou o telefone dela pelo da mãe. Nenhum desses passos olhava se a
/// chave trazia a mesma pessoa. Cada um, sozinho, "só completava" — juntos, fundiram duas.</para>
///
/// <para><b>Por que os DOIS sinais, e não um.</b> Nascimento diferente com o mesmo nome é o erro
/// de digitação comum — o upsert canônico já congela e manda para a arbitragem (ADR-0039 §3.2), e
/// isso continua. Nome diferente com o mesmo nascimento é nome social, casamento, abreviação.
/// Os dois juntos é que não têm explicação de mesma pessoa: é o filho com o documento do
/// responsável, o "RN DE…" com o CPF da mãe, o CPF digitado errado que caiu em outro cidadão.
/// Na dúvida a resposta é "não sei" (<c>false</c>) — quem chama segue como hoje.</para>
///
/// <para>Estático e sem dependência, como <see cref="GuardaIdentidadeCadastro"/>: a decisão é a
/// parte que precisa de teste.</para>
/// </summary>
public static class ConferenciaPessoa
{
    /// <summary>Partículas que não identificam ninguém ("MARIA DA SILVA" = "MARIA SILVA").</summary>
    private static readonly HashSet<string> Particulas = ["DE", "DA", "DO", "DAS", "DOS", "E", "DI", "DU", "D"];

    /// <summary>Primeiro token de quem ainda não tem nome próprio: "RN DE MARIA", "RECEM NASCIDO DE…".</summary>
    private static readonly HashSet<string> MarcasRecemNascido = ["RN", "RECEM", "NATIMORTO"];

    /// <summary>
    /// <c>true</c> só quando nome E nascimento dizem que são duas pessoas. Qualquer lado sem nome
    /// ou sem nascimento conhecido devolve <c>false</c>: sem os dois sinais não dá para afirmar.
    /// </summary>
    public static bool SaoPessoasDiferentes(
        string? nomeA, DateOnly? nascimentoA, string? nomeB, DateOnly? nascimentoB)
    {
        if (!Conhecido(nascimentoA) || !Conhecido(nascimentoB) || nascimentoA == nascimentoB) return false;

        var a = Tokens(nomeA);
        var b = Tokens(nomeB);
        if (a.Count == 0 || b.Count == 0) return false;

        // "RN DE MARIA SILVA" não é a Maria: é o filho dela, registrado antes de ter nome — e é
        // justamente o recém-nascido que costuma herdar o CPF/CNS da mãe no cadastro hospitalar.
        var rnA = MarcasRecemNascido.Contains(a[0]);
        var rnB = MarcasRecemNascido.Contains(b[0]);
        if (rnA != rnB) return true;
        if (rnA) { a = [.. a.Skip(1)]; b = [.. b.Skip(1)]; }
        if (a.Count == 0 || b.Count == 0) return false;

        return !NomesCompativeis(a, b);
    }

    /// <summary>Atalho para quem tem o nascimento como texto (FHIR <c>yyyy-MM-dd</c>, e-SUS).</summary>
    public static bool SaoPessoasDiferentes(string? nomeA, string? nascimentoA, string? nomeB, string? nascimentoB) =>
        SaoPessoasDiferentes(nomeA, Data(nascimentoA), nomeB, Data(nascimentoB));

    /// <summary>"NOME (nasc. dd/MM/aaaa)" — o jeito de mostrar ao operador quem é quem.</summary>
    public static string Descrever(string? nome, DateOnly? nascimento) =>
        $"{(string.IsNullOrWhiteSpace(nome) ? "(sem nome)" : nome.Trim())}"
        + (Conhecido(nascimento) ? $" (nasc. {nascimento!.Value:dd/MM/yyyy})" : string.Empty);

    /// <summary>Data em <c>yyyy-MM-dd</c> (FHIR) ou <c>dd/MM/yyyy</c>; parcial ou inválida = desconhecida.</summary>
    public static DateOnly? Data(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        if (t.Length > 10) t = t[..10]; // "1994-11-23T00:00:00" → só a data
        return DateOnly.TryParseExact(t, ["yyyy-MM-dd", "dd/MM/yyyy", "dd.MM.yyyy"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    /// <summary>
    /// O nascimento é informação, e não preenchimento? <c>default</c> (0001-01-01) é o que entra
    /// quando a fonte não disse nada; antes de 1900 é placeholder de sistema.
    /// </summary>
    private static bool Conhecido(DateOnly? d) => d is { Year: >= 1900 };

    /// <summary>
    /// Os nomes podem ser da mesma pessoa? Basta o primeiro nome de um aparecer (parecido) em
    /// qualquer posição do outro — cobre a inversão ("GRACAS MARIA" × "MARIA DAS GRACAS") e o
    /// primeiro nome omitido ("JOSE DA SILVA" × "MARIA JOSE DA SILVA"). É generoso de propósito:
    /// erro aqui para o lado do "compatível" só mantém o comportamento de hoje.
    /// </summary>
    private static bool NomesCompativeis(List<string> a, List<string> b) =>
        b.Any(t => Parecidos(a[0], t)) || a.Any(t => Parecidos(b[0], t));

    private static bool Parecidos(string x, string y)
    {
        if (x == y) return true;
        // Inicial ("M. JOSE") casa com qualquer nome que comece pela mesma letra.
        if (x.Length == 1 || y.Length == 1) return x[0] == y[0];
        var fx = Fonetico(x);
        var fy = Fonetico(y);
        if (fx == fy) return true;
        var tolerancia = Math.Min(fx.Length, fy.Length) >= 8 ? 2 : 1;
        return Distancia(fx, fy) <= tolerancia;
    }

    /// <summary>
    /// Grafias que soam iguais em português viram uma só: THAIS = TAIS, KAUANY = CAUANI,
    /// LUIZ = LUIS, ANNA = ANA. Não é Soundex — é só o suficiente para a digitação de recepção.
    /// </summary>
    private static string Fonetico(string s)
    {
        var t = s.Replace("PH", "F").Replace("TH", "T").Replace("Y", "I").Replace("W", "V")
            .Replace("K", "C").Replace("Z", "S");
        var sb = new StringBuilder(t.Length);
        for (var i = 0; i < t.Length; i++)
        {
            var c = t[i];
            // H mudo some; o de CH/LH/NH fica (CHARLES ≠ CARLES).
            if (c == 'H' && (i == 0 || t[i - 1] is not ('C' or 'L' or 'N'))) continue;
            if (sb.Length > 0 && sb[^1] == c) continue; // letra dobrada: ANNA = ANA
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static int Distancia(string a, string b)
    {
        var anterior = new int[b.Length + 1];
        var atual = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) anterior[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            atual[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var custo = a[i - 1] == b[j - 1] ? 0 : 1;
                atual[j] = Math.Min(Math.Min(atual[j - 1] + 1, anterior[j] + 1), anterior[j - 1] + custo);
            }
            (anterior, atual) = (atual, anterior);
        }
        return anterior[b.Length];
    }

    /// <summary>Maiúsculas, sem acento, só letras, sem partículas.</summary>
    private static List<string> Tokens(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return [];
        var sb = new StringBuilder(nome.Length);
        foreach (var c in nome.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetter(c) ? char.ToUpperInvariant(c) : ' ');
        }
        return [.. sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => !Particulas.Contains(t))];
    }
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace SMSMais.Core.Integracoes.KlinikosWeb;

/// <summary>
/// Lê os relatórios do Klinikos exportados como XLS BIFF (o <c>rptviewXls.aspx</c>, gerado pelo
/// Crystal). Espelha o método de <c>PlanilhaSerParser</c>: <b>cabeçalho por NOME de coluna,
/// nunca por posição</b> (a planilha tem células mescladas e o índice não alinha linha a linha).
///
/// <para>Armadilhas medidas (Automais.klinikos/docs/dados-crystal.md §E):</para>
/// <list type="bullet">
///   <item><c>spa_codigo</c> vem 11 dígitos no 407 (o Excel comeu o zero à esquerda) e 12 no 667;
///     normalizar SEMPRE para 12 (<c>PadLeft</c>), senão o JOIN 407×667 dá zero overlap.</item>
///   <item>No 667 a COR é <b>cabeçalho de grupo</b> (linha só com a cor), não coluna — rastrear a
///     cor corrente ao varrer.</item>
/// </list>
/// </summary>
internal static partial class KlinikosRelatorioParser
{
    private const int LinhasDeTopoParaProcurarCabecalho = 20;

    static KlinikosRelatorioParser()
    {
        // BIFF guarda texto em code page (windows-1252). Sem o provider o ExcelDataReader lança
        // "No data is available for encoding 1252" no .NET moderno.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [GeneratedRegex("^(vermelh|amarel|verde|laranja|azul)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexCor();

    // ------------------------------------------------------------------ 407

    /// <summary>407 — Pacientes Registrados no Dia. Colunas: Nº Boletim, Prontuário, Paciente,
    /// Dt. Nascimento/Idade, Clínica.</summary>
    public static IReadOnlyList<BoletimRegistro> Ler407(byte[] xls)
    {
        var (colunas, linhas) = LerGrade(xls, new()
        {
            ["boletim"] = "boletim",
            ["no boletim"] = "boletim",
            ["n boletim"] = "boletim",
            ["numero do boletim"] = "boletim",
            ["prontuario"] = "prontuario",
            ["paciente"] = "paciente",
            ["nome"] = "paciente",
            ["dt nascimento idade"] = "nascimento",
            ["dt nascimento"] = "nascimento",
            ["nascimento"] = "nascimento",
            ["clinica"] = "clinica",
        });

        var saida = new List<BoletimRegistro>();
        foreach (var celulas in linhas)
        {
            var spa = NormalizarBoletim(Campo(colunas, celulas, "boletim"));
            if (spa is null) continue;
            saida.Add(new BoletimRegistro(
                spa,
                Campo(colunas, celulas, "prontuario"),
                Campo(colunas, celulas, "paciente"),
                Campo(colunas, celulas, "nascimento"),
                Campo(colunas, celulas, "clinica")));
        }
        return saida;
    }

    // ------------------------------------------------------------------ 667

    /// <summary>667 — Nominal por Classificação de Risco. Colunas: Número do Boletim, Paciente,
    /// Idade, Data/Hora Entrada, Clínica, Origem. A COR é cabeçalho de grupo.</summary>
    public static IReadOnlyList<ClassificacaoRegistro> Ler667(byte[] xls)
    {
        var (colunas, linhas) = LerGrade(xls, new()
        {
            ["numero do boletim"] = "boletim",
            ["no boletim"] = "boletim",
            ["boletim"] = "boletim",
            ["paciente"] = "paciente",
            ["idade"] = "idade",
            ["data hora entrada"] = "entrada",
            ["data hora de entrada"] = "entrada",
            ["entrada"] = "entrada",
            ["clinica"] = "clinica",
            ["origem"] = "origem",
        });

        var saida = new List<ClassificacaoRegistro>();
        string? corAtual = null;
        foreach (var celulas in linhas)
        {
            // Linha-título de grupo: uma única célula não-vazia começando por uma cor.
            var naoVazias = celulas.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            if (naoVazias.Count == 1 && RegexCor().IsMatch(naoVazias[0]!.Trim()))
            {
                corAtual = naoVazias[0]!.Trim();
                continue;
            }

            var spa = NormalizarBoletim(Campo(colunas, celulas, "boletim"));
            if (spa is null) continue;
            saida.Add(new ClassificacaoRegistro(
                spa,
                Campo(colunas, celulas, "entrada"),
                corAtual,
                Campo(colunas, celulas, "origem")));
        }
        return saida;
    }

    // ------------------------------------------------------------------ 526

    [GeneratedRegex(@"^\s*CID\s*[:\-]", RegexOptions.IgnoreCase)]
    private static partial Regex RegexCidSublinha();

    /// <summary>
    /// 526 — Atendimentos por Profissional. Colunas da linha principal: No Boletim, Código, Hora
    /// Atendimento, Nome Paciente, Classificação (veio vazia), Idade, Sexo, Clínica. Sob cada
    /// boletim há sub-linhas <c>CID: &lt;texto&gt;</c> e <c>PROCEDIMENTOS: ...</c>. O CID é o valor
    /// aqui — o único jeito em lote de ter diagnóstico por boletim.
    /// </summary>
    public static IReadOnlyList<AtendimentoRegistro> Ler526(byte[] xls)
    {
        var (colunas, linhas) = LerGrade(xls, new()
        {
            ["no boletim"] = "boletim",
            ["numero do boletim"] = "boletim",
            ["boletim"] = "boletim",
            ["hora atendimento"] = "hora",
            ["hora"] = "hora",
        });

        var saida = new List<AtendimentoRegistro>();
        string? spaAtual = null, horaAtual = null, cidAtual = null;

        void Fechar()
        {
            if (spaAtual is not null)
            {
                saida.Add(new AtendimentoRegistro(spaAtual, horaAtual, cidAtual));
            }
        }

        foreach (var celulas in linhas)
        {
            // Sub-linha "CID: ..." — pertence ao boletim corrente.
            var primeira = celulas.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim();
            if (primeira is not null && spaAtual is not null && cidAtual is null && RegexCidSublinha().IsMatch(primeira))
            {
                cidAtual = primeira[(primeira.IndexOfAny([':', '-']) + 1)..].Trim();
                continue;
            }

            var spa = NormalizarBoletim(Campo(colunas, celulas, "boletim"));
            if (spa is null) continue;

            Fechar();
            spaAtual = spa;
            horaAtual = Campo(colunas, celulas, "hora");
            cidAtual = null;
        }
        Fechar();
        return saida;
    }

    // ------------------------------------------------------------------ 751 (build 2025)

    /// <summary>
    /// 751 — Nominal (build 2025, UPA/Santa Rita): "Estatística de Atendimento". Colunas medidas:
    /// Nº Boletim, Código (prontuário), Hora Atendimento, Tipo, Nome Paciente, Idade, Sexo. Só
    /// boletins ATENDIDOS. <b>Não traz chegada nem cor</b> (nesta build vêm da fila viva/deep) —
    /// por isso o registro sai com <c>Clinica</c> nula e a idade em <c>NascimentoIdade</c>.
    /// </summary>
    public static IReadOnlyList<BoletimRegistro> Ler751(byte[] xls)
    {
        var (colunas, linhas) = LerGrade(xls, new()
        {
            ["no boletim"] = "boletim",
            ["n boletim"] = "boletim",
            ["numero do boletim"] = "boletim",
            ["boletim"] = "boletim",
            ["codigo"] = "prontuario",
            ["nome paciente"] = "paciente",
            ["paciente"] = "paciente",
            ["nome"] = "paciente",
            ["idade"] = "idade",
        });

        var saida = new List<BoletimRegistro>();
        foreach (var celulas in linhas)
        {
            var spa = NormalizarBoletim(Campo(colunas, celulas, "boletim"));
            if (spa is null) continue;
            saida.Add(new BoletimRegistro(
                spa,
                Campo(colunas, celulas, "prontuario"),
                Campo(colunas, celulas, "paciente"),
                Campo(colunas, celulas, "idade"),
                Clinica: null));
        }
        return saida;
    }

    // ------------------------------------------------------------------ 752 (build 2025)

    /// <summary>
    /// 752 — Por CID (build 2025): = 751 + coluna CID (texto do diagnóstico, SEM código ICD-10 →
    /// exige o de-para texto→código). Um registro por linha com boletim; o CID vem da COLUNA, não
    /// de sub-linha (diferente do 526 do Conde).
    /// </summary>
    public static IReadOnlyList<AtendimentoRegistro> Ler752(byte[] xls)
    {
        var (colunas, linhas) = LerGrade(xls, new()
        {
            ["no boletim"] = "boletim",
            ["n boletim"] = "boletim",
            ["numero do boletim"] = "boletim",
            ["boletim"] = "boletim",
            ["hora atendimento"] = "hora",
            ["hora"] = "hora",
            ["cid"] = "cid",
        });

        var saida = new List<AtendimentoRegistro>();
        foreach (var celulas in linhas)
        {
            var spa = NormalizarBoletim(Campo(colunas, celulas, "boletim"));
            if (spa is null) continue;
            saida.Add(new AtendimentoRegistro(
                spa,
                Campo(colunas, celulas, "hora"),
                Campo(colunas, celulas, "cid")));
        }
        return saida;
    }

    // ------------------------------------------------------------------ base

    /// <summary>
    /// Varre a planilha, acha a linha de cabeçalho (a primeira que reconhece a coluna "boletim"
    /// pelos sinônimos) e devolve o mapa de colunas + as linhas de dado (todas as linhas após o
    /// cabeçalho, como arrays de célula). O filtro de "é linha de dado" fica com cada leitor
    /// (407/667), que sabe qual coluna é a chave.
    /// </summary>
    private static (Dictionary<string, int> Colunas, List<string?[]> Linhas) LerGrade(
        byte[] xls, Dictionary<string, string> sinonimos)
    {
        using var ms = new MemoryStream(xls, writable: false);
        using var reader = ExcelReaderFactory.CreateReader(ms);

        Dictionary<string, int>? colunas = null;
        var linhas = new List<string?[]>();
        var lidas = 0;

        while (reader.Read())
        {
            var celulas = new string?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++) celulas[i] = Valor(reader.GetValue(i));

            if (colunas is null)
            {
                lidas++;
                var candidato = MapearCabecalho(celulas, sinonimos);
                if (candidato.ContainsKey("boletim"))
                {
                    colunas = candidato;
                }
                else if (lidas >= LinhasDeTopoParaProcurarCabecalho)
                {
                    break;
                }
                continue;
            }

            linhas.Add(celulas);
        }

        if (colunas is null)
        {
            throw new InvalidOperationException(
                "O relatório do Klinikos não tem cabeçalho com a coluna 'Boletim' reconhecível "
                + $"(procurei nas {LinhasDeTopoParaProcurarCabecalho} primeiras linhas). O layout mudou?");
        }

        return (colunas, linhas);
    }

    private static Dictionary<string, int> MapearCabecalho(string?[] celulas, Dictionary<string, string> sinonimos)
    {
        var mapa = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < celulas.Length; i++)
        {
            var nome = Normalizar(celulas[i]);
            if (nome.Length == 0) continue;
            if (sinonimos.TryGetValue(nome, out var chave) && !mapa.ContainsKey(chave)) mapa[chave] = i;
        }
        return mapa;
    }

    private static string? Campo(Dictionary<string, int> colunas, string?[] celulas, string chave) =>
        colunas.TryGetValue(chave, out var i) && i < celulas.Length
            ? (string.IsNullOrWhiteSpace(celulas[i]) ? null : celulas[i])
            : null;

    /// <summary>
    /// Normaliza o Nº Boletim: só dígitos, <c>PadLeft(12,'0')</c>. Recusa o que não parece boletim
    /// (menos de 9 dígitos = totalizador/ruído). É a correção da armadilha do zero à esquerda.
    /// </summary>
    internal static string? NormalizarBoletim(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;
        var digitos = new string(bruto.Where(char.IsDigit).ToArray());
        if (digitos.Length is < 9 or > 12) return null;
        return digitos.PadLeft(12, '0');
    }

    /// <summary>Célula do BIFF → texto. Números viram dígitos sem notação científica (o boletim
    /// chega como <c>double</c> no 407); datas viram <c>dd/MM/yyyy HH:mm</c>.</summary>
    private static string? Valor(object? bruto) => bruto switch
    {
        null => null,
        DateTime d => d.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
        double n => n == Math.Floor(n) && Math.Abs(n) < 1e15
            ? ((long)n).ToString(CultureInfo.InvariantCulture)
            : n.ToString("0.##########", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        _ => bruto.ToString()?.Trim(),
    };

    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var semAcento = new string(
            texto.Normalize(NormalizationForm.FormD)
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
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                espaco = true;
            }
        }
        return sb.ToString();
    }
}

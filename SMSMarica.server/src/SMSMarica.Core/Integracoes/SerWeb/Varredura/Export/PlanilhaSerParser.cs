using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace SMSMarica.Core.Integracoes.SerWeb.Varredura.Export;

/// <summary>
/// Lê o <c>historico-pesquisar.xls</c> que o botão Exportar do SER devolve — BIFF8 dentro de um
/// arquivo composto OLE2, ~192 KB para 500 linhas.
///
/// <para><b>Mapeamento por NOME de coluna, nunca por posição.</b> A planilha é gerada pela SES-RJ
/// e ninguém nos avisa quando uma coluna entra no meio; casar por índice faria CNS virar CID sem
/// erro nenhum. Pelo mesmo motivo a linha de cabeçalho é <i>procurada</i> (a grade da tela tem uma
/// linha de título mesclado antes dela) em vez de assumida na posição 0.</para>
/// </summary>
internal static class PlanilhaSerParser
{
    /// <summary>Quantas linhas do topo podem ser título/decoração antes do cabeçalho real.</summary>
    private const int LinhasDeTopoParaProcurarCabecalho = 8;

    /// <summary>Mínimo de colunas reconhecidas para aceitar uma linha como cabeçalho. Abaixo disso
    /// é ruído, e seguir em frente importaria lixo silenciosamente.</summary>
    private const int ColunasReconhecidasMinimo = 4;

    static PlanilhaSerParser()
    {
        // BIFF8 guarda texto em code page (o SER usa windows-1252). Sem este provider o
        // ExcelDataReader lança "No data is available for encoding 1252" no .NET moderno.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static IReadOnlyList<SerLinhaGrade> Ler(byte[] planilha)
    {
        using var ms = new MemoryStream(planilha, writable: false);
        using var reader = ExcelReaderFactory.CreateReader(ms);

        var linhas = new List<SerLinhaGrade>();
        Dictionary<string, int>? colunas = null;
        var lidas = 0;

        while (reader.Read())
        {
            var celulas = new string?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++) celulas[i] = Valor(reader.GetValue(i));

            if (colunas is null)
            {
                lidas++;
                var candidato = MapearCabecalho(celulas);
                if (candidato.Count >= ColunasReconhecidasMinimo)
                {
                    GarantirColunasEstruturais(candidato);
                    colunas = candidato;
                }
                else if (lidas >= LinhasDeTopoParaProcurarCabecalho)
                {
                    // >= e não >: com >, a janela efetiva era 9 linhas enquanto a constante e a
                    // mensagem de erro diziam 8 — diagnóstico que mente por um.
                    break;
                }
                continue;
            }

            var id = Campo(colunas, celulas, "id");
            // O ID é o discriminador: linha de rodapé, cabeçalho repetido ou totalizador não têm
            // ID numérico. Sem esse filtro, entraria lixo com IdSer vazio no espelho.
            if (string.IsNullOrWhiteSpace(id) || !id.All(char.IsDigit)) continue;

            linhas.Add(new SerLinhaGrade
            {
                IdSer = id,
                Tipo = Campo(colunas, celulas, "tipo"),
                Recurso = Campo(colunas, celulas, "recurso"),
                DataSolicitacao = Data(colunas, celulas, "data_solicitacao"),
                Paciente = Campo(colunas, celulas, "paciente"),
                Idade = Campo(colunas, celulas, "idade"),
                Cpf = Campo(colunas, celulas, "cpf"),
                Cns = Campo(colunas, celulas, "cns"),
                Cid = Campo(colunas, celulas, "cid"),
                Solicitante = Campo(colunas, celulas, "solicitante"),
                MunicipioSolicitante = Campo(colunas, celulas, "municipio_solicitante"),
                UnidadeExecutora = Campo(colunas, celulas, "unidade_executora"),
                AgendadoPara = Data(colunas, celulas, "agendado_para"),
                Situacao = Campo(colunas, celulas, "situacao"),
            });
        }

        if (colunas is null)
        {
            throw new InvalidOperationException(
                "A planilha exportada pelo SER não tem uma linha de cabeçalho reconhecível "
                + $"(procurei nas {LinhasDeTopoParaProcurarCabecalho} primeiras linhas). O layout do "
                + "export mudou — conferir docs/ser.md antes de confiar em qualquer carga.");
        }

        return linhas;
    }

    // ------------------------------------------------------------------ cabeçalho

    /// <summary>Nome normalizado da coluna na planilha → chave interna.</summary>
    private static readonly Dictionary<string, string> Sinonimos = new(StringComparer.Ordinal)
    {
        ["id"] = "id",
        ["id solicitacao"] = "id",
        ["id da solicitacao"] = "id",
        ["numero da solicitacao"] = "id",
        ["tipo"] = "tipo",
        ["recurso"] = "recurso",
        ["data da solicitacao"] = "data_solicitacao",
        ["data solicitacao"] = "data_solicitacao",
        ["paciente"] = "paciente",
        ["nome do paciente"] = "paciente",
        ["nome"] = "paciente",
        ["idade"] = "idade",
        ["cpf"] = "cpf",
        ["cns"] = "cns",
        ["cid"] = "cid",
        ["solicitante"] = "solicitante",
        ["unidade solicitante"] = "solicitante",
        ["municipio solicitante"] = "municipio_solicitante",
        ["municipio do solicitante"] = "municipio_solicitante",
        ["unidade executora"] = "unidade_executora",
        ["unidade executante"] = "unidade_executora",
        ["data do agendamento"] = "agendado_para",
        ["data de agendamento"] = "agendado_para",
        ["agendado para"] = "agendado_para",
        ["situacao"] = "situacao",
    };

    /// <summary>
    /// Cabeçalho aceito precisa das colunas ESTRUTURAIS, não de 4 quaisquer.
    ///
    /// <para>Sem a coluna de ID, o filtro de "linha de dado tem ID numérico" descartaria TODAS as
    /// linhas e <c>Ler</c> devolveria vazio sem exceção — que o varredor leria como "trecho folgado
    /// da linha do tempo", aplicando lote vazio e avançando o cursor. Sem a de data, tudo entraria
    /// sem o eixo do fatiamento. Um renomeio da SES-RJ fora dos sinônimos precisa PARAR a carga
    /// com nome e sobrenome, não escoar como cobertura. Achado do repasse de 07/08/2026.</para>
    /// </summary>
    internal static void GarantirColunasEstruturais(Dictionary<string, int> colunas)
    {
        var faltando = new List<string>(2);
        if (!colunas.ContainsKey("id")) faltando.Add("ID Solicitação");
        if (!colunas.ContainsKey("data_solicitacao")) faltando.Add("Data da Solicitação");
        if (faltando.Count == 0) return;

        throw new InvalidOperationException(
            "A planilha do SER tem um cabeçalho reconhecível, mas sem coluna(s) estrutural(is): "
            + $"{string.Join(" e ", faltando)}. A SES-RJ renomeou a coluna? Atualize os sinônimos "
            + "do PlanilhaSerParser — importar sem elas perderia a carga em silêncio.");
    }

    private static Dictionary<string, int> MapearCabecalho(string?[] celulas)
    {
        var mapa = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < celulas.Length; i++)
        {
            var nome = Normalizar(celulas[i]);
            if (nome.Length == 0) continue;
            if (Sinonimos.TryGetValue(nome, out var chave) && !mapa.ContainsKey(chave)) mapa[chave] = i;
        }
        return mapa;
    }

    /// <summary>Menor e maior serial aceitos: 1990-01-01 e 2100-01-01. Fora disso é outra coisa
    /// travestida de número (CNS tem 15 dígitos, ID tem 7).</summary>
    private const double SerialMinimo = 32874;
    private const double SerialMaximo = 73051;

    /// <summary>
    /// Coluna de data → <c>dd/MM/yyyy</c>.
    ///
    /// <para><b>O SER exporta data como número de série do Excel</b> (medido em 06/08/2026:
    /// <c>46204.37281195602</c> = 01/07/2026 08:56), não como texto nem como data formatada. Sem
    /// converter, a solicitação entraria no espelho <b>sem data de solicitação</b> — que é o eixo
    /// de todo o fatiamento — e sem erro nenhum, porque o parse de "46204,37" só devolve nulo.</para>
    ///
    /// <para>A conversão é feita SÓ nas colunas de data: o ID também chega como número
    /// (<c>8000763.0</c>) e virar data seria pior que ficar nulo.</para>
    /// </summary>
    private static string? Data(Dictionary<string, int> colunas, string?[] celulas, string chave) =>
        ConverterData(Campo(colunas, celulas, chave));

    /// <summary>Exposto para teste: a conversão do serial é a parte que, errada, some com a data
    /// sem levantar erro.</summary>
    internal static string? ConverterData(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;

        // Já veio no formato certo (ExcelDataReader reconheceu o formato de data da célula).
        if (DateOnly.TryParseExact(bruto, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
        {
            return bruto;
        }

        if (double.TryParse(bruto, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
            && serial is >= SerialMinimo and <= SerialMaximo)
        {
            return DateTime.FromOADate(serial).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        return bruto;
    }

    private static string? Campo(Dictionary<string, int> colunas, string?[] celulas, string chave) =>
        colunas.TryGetValue(chave, out var i) && i < celulas.Length
            ? (string.IsNullOrWhiteSpace(celulas[i]) ? null : celulas[i])
            : null;

    // ------------------------------------------------------------------ células

    /// <summary>
    /// Célula do BIFF → texto. Datas viram <c>dd/MM/yyyy</c> (o resto do motor fala esse formato) e
    /// números viram dígitos sem notação científica: <b>ID e CNS chegam como <c>double</c></b>, e
    /// <c>ToString()</c> puro devolveria "3,96862E+06" — que passaria no filtro de dígitos como
    /// lixo se não fosse tratado aqui.
    /// </summary>
    private static string? Valor(object? bruto) => bruto switch
    {
        null => null,
        DateTime d => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
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

        var limpo = new StringBuilder(semAcento.Length);
        var espacoPendente = false;
        foreach (var c in semAcento)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (espacoPendente && limpo.Length > 0) limpo.Append(' ');
                espacoPendente = false;
                limpo.Append(char.ToLowerInvariant(c));
            }
            else
            {
                espacoPendente = true;
            }
        }
        return limpo.ToString();
    }
}

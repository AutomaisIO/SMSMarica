using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SMSMais.Core.Integracoes.SisregWeb.Fila;

/// <summary>Uma pessoa esperando na fila do SISREG, como o <c>gerenciador_solicitacao</c> a lista.</summary>
/// <param name="Risco">0 = vermelho (mais urgente) … 3 = azul. Nulo quando o SISREG não classificou.</param>
public sealed record LinhaFilaPendente(
    string CodigoSolicitacao,
    DateOnly? DataSolicitacao,
    int? Risco,
    string? PacienteNome,
    string? Cns,
    string? NomeMae,
    DateOnly? DataNascimento,
    int? IdadeAnos,
    string? Telefone,
    string? Municipio,
    string? ProcedimentoCodigo,
    string? ProcedimentoNome,
    string? CidCodigo,
    string? UnidadeSolicitante,
    string? Situacao);

/// <summary>
/// Lê a listagem de <c>gerenciador_solicitacao?etapa=LISTAR_SOLICITACOES</c> — a fila de quem
/// pediu e <b>ainda não foi agendado</b>.
///
/// <para><b>Escrito contra HTML real</b> (capturas de 05/09/2026 no laboratório: 352, 3.669 e
/// 15.505 linhas). Duas coisas que a documentação do laboratório dava como ausentes e estão lá —
/// e é por elas que esta tela consegue ser uma lista de espera de verdade:</para>
/// <list type="bullet">
///   <item><b>O CNS vem</b>, no atributo <c>title</c> da célula do paciente, junto com nome da mãe
///   e data de nascimento — em <b>100%</b> das linhas conferidas. O doc dizia que a identidade só
///   sairia numa requisição por pessoa (inviável para 15 mil). Com o CNS na listagem, a pessoa da
///   fila casa com o paciente do hub pela régua de sempre (ADR-0041 / dedup por CPF-CNS).</item>
///   <item><b>O risco vem</b>, como imagem mais um <c>title</c> numérico (0 vermelho … 3 azul).
///   O doc dizia "sempre vazio" — na amostra de 352 havia 10 vermelhos.</item>
/// </list>
///
/// <para><b>Por que regex e não um parser de DOM.</b> A saída é HTML4 de CGI, com tags em caixa
/// alta, atributos sem aspas (<c>&lt;img src=/imagens/verde.png&gt;</c>) e sem fechamento — um
/// parser estrito recusa; um tolerante custaria uma dependência para ler 12 células de posição
/// fixa. O formato é de 2008 e não muda.</para>
/// </summary>
public static partial class FilaPendenteHtmlParser
{
    /// <summary>Uma linha de dados: só a que tem <c>onClick="visualizaFicha(N)"</c> com número —
    /// as demais ocorrências do nome estão na definição da função JS, no topo da página.</summary>
    [GeneratedRegex(@"<TR onClick=""visualizaFicha\(\d+\);""[^>]*>(.*?)</TR>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinhaRegex();

    [GeneratedRegex(@"<TD([^>]*)>(.*?)</TD>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CelulaRegex();

    [GeneratedRegex(@"title=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    public static IReadOnlyList<LinhaFilaPendente> Ler(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];

        var linhas = new List<LinhaFilaPendente>();

        foreach (Match linha in LinhaRegex().Matches(html))
        {
            var celulas = CelulaRegex().Matches(linha.Groups[1].Value);

            // 12 colunas de posição fixa. Linha com menos é cabeçalho, rodapé ou lixo — pular é
            // melhor que adivinhar: uma coluna deslocada gravaria telefone no lugar de idade.
            if (celulas.Count < 12) continue;

            var codigo = Texto(celulas[0]);
            if (string.IsNullOrWhiteSpace(codigo)) continue;

            var (cns, nomeTitle, mae, nascimento) = LerFichaDoTitle(Atributos(celulas[3]));
            var (procCodigo, procNome) = SepararProcedimento(Texto(celulas[7]));

            linhas.Add(new LinhaFilaPendente(
                codigo,
                Data(Texto(celulas[1])),
                RiscoDoTitle(Atributos(celulas[2])),
                // O texto da célula é o nome; o title repete. Fica o do title quando o texto vier
                // vazio (acontece quando o SISREG corta o nome muito longo na exibição).
                Vazio(Texto(celulas[3])) ?? nomeTitle,
                cns,
                mae,
                nascimento,
                Idade(Texto(celulas[6])),
                // Pode vir mais de um separado por <br>: guardamos a linha inteira normalizada em
                // vez de escolher um — quem liga decide, e descartar número é perder contato.
                Vazio(Texto(celulas[4]).Replace("<br>", " / ", StringComparison.OrdinalIgnoreCase)),
                Vazio(Texto(celulas[5])),
                procCodigo,
                procNome,
                Vazio(Texto(celulas[8])),
                Vazio(Texto(celulas[9])),
                Vazio(Texto(celulas[11]))));
        }

        return linhas;
    }

    /// <summary>Texto visível da célula, sem tags e com entidades resolvidas.</summary>
    private static string Texto(Match celula)
    {
        var bruto = celula.Groups[2].Value;
        // <br> vira marcador antes de as tags caírem, senão dois telefones grudam num só número.
        bruto = Regex.Replace(bruto, "<br\\s*/?>", "<br>", RegexOptions.IgnoreCase);
        var semTags = TagRegex().Replace(bruto.Replace("<br>", ""), " ").Replace('', '\n');
        return WebUtility.HtmlDecode(semTags).Replace(' ', ' ').Trim();
    }

    private static string Atributos(Match celula) => celula.Groups[1].Value;

    private static string? Vazio(string? v)
    {
        var t = v?.Replace('\n', ' ').Trim();
        // "---" é como o SISREG escreve "não se aplica" (unidade executante de quem não foi
        // regulado ainda). Guardar o traço faria a coluna parecer preenchida.
        return string.IsNullOrWhiteSpace(t) || t == "---" ? null : t;
    }

    /// <summary>
    /// A ficha do paciente mora no <c>title</c> da célula, em linhas <c>Rótulo: valor</c>.
    /// É daqui que sai o CNS — o dado que liga esta fila ao nosso cadastro.
    /// </summary>
    private static (string? Cns, string? Nome, string? Mae, DateOnly? Nascimento) LerFichaDoTitle(
        string atributos)
    {
        var m = TitleRegex().Match(atributos);
        if (!m.Success) return (null, null, null, null);

        var t = WebUtility.HtmlDecode(m.Groups[1].Value);

        return (
            Vazio(Campo(t, "Número CNS") ?? Campo(t, "Numero CNS")),
            Vazio(Campo(t, "Nome Paciente")),
            Vazio(Campo(t, "Nome da Mãe") ?? Campo(t, "Nome da Mae")),
            Data(Campo(t, "Data Nascimento")));
    }

    /// <summary>Valor de um rótulo dentro do title, até a quebra de linha ou o próximo rótulo.</summary>
    private static string? Campo(string texto, string rotulo)
    {
        var i = texto.IndexOf(rotulo + ":", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;

        var inicio = i + rotulo.Length + 1;
        var fim = texto.IndexOfAny(['\n', '\r'], inicio);
        return (fim < 0 ? texto[inicio..] : texto[inicio..fim]).Trim();
    }

    /// <summary>
    /// Risco: o <c>title</c> da célula é o número (0 vermelho … 3 azul); a cor está numa imagem.
    /// Fica o número — cor é apresentação, e o nosso lado escolhe a dele.
    /// </summary>
    private static int? RiscoDoTitle(string atributos)
    {
        var m = TitleRegex().Match(atributos);
        return m.Success && int.TryParse(m.Groups[1].Value.Trim(), out var r) && r is >= 0 and <= 9
            ? r
            : null;
    }

    /// <summary>
    /// Separa código e nome do procedimento.
    ///
    /// <para><b>Na prática o código NÃO vem</b> nesta listagem: as 15.502 linhas conferidas trazem
    /// só o nome ("GRUPO - PEQUENAS CIRURGIAS - LOCAL", "CONSULTA EM OFTALMOLOGIA - PEDIATRA").
    /// O hífen faz parte do nome, não separa código de descrição — por isso a exigência de que o
    /// prefixo seja todo dígito: sem ela, "GRUPO" viraria código de procedimento.</para>
    ///
    /// <para>Consequência de projeto: <b>casar a fila com a oferta é pelo NOME</b>, que já é a
    /// régua da casa para o SISREG. O caminho com código fica para o dia em que o SISREG passe a
    /// mandá-lo.</para>
    /// </summary>
    private static (string? Codigo, string? Nome) SepararProcedimento(string texto)
    {
        var t = texto.Replace('\n', ' ').Trim();
        if (t.Length == 0) return (null, null);

        var i = t.IndexOf(" - ", StringComparison.Ordinal);
        if (i < 0) return (null, t);

        var codigo = t[..i].Trim();
        var resto = t[(i + 3)..].Trim();
        // Só é código se for dígito: procedimento sem código na frente não pode virar código falso.
        return codigo.All(char.IsDigit) && codigo.Length > 0 ? (codigo, Vazio(resto)) : (null, Vazio(t));
    }

    private static DateOnly? Data(string? v) =>
        DateOnly.TryParseExact(v?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d)
            ? d
            : null;

    /// <summary>"53 anos" / "1 ano" → 53 / 1.</summary>
    private static int? Idade(string texto)
    {
        var m = Regex.Match(texto, @"(\d+)");
        return m.Success && int.TryParse(m.Groups[1].Value, out var i) && i is >= 0 and < 130 ? i : null;
    }
}

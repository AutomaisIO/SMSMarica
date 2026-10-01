using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using AngleSharp.Dom;
using AngleSharp.Html.Dom;

using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Integracoes.SerWeb.Profissionais;

/// <summary>Uma linha da pesquisa de profissionais do SER, crua.</summary>
public sealed record SerProfissionalLinha(
    string? Cpf, string? Documento, string? TipoDocumento, string Nome, bool Ativo);

/// <summary>
/// Lê a pesquisa de <b>Cadastro → Profissionais</b> do SER, inteira (ADR-0065).
///
/// <para><b>SOMENTE LEITURA</b>: abre a tela, aperta Pesquisar com o filtro vazio e percorre o
/// datascroller. Tudo passa por <see cref="ISerWebSessao.SubmeterFormAsync"/>, cuja trava
/// recusa verbo de escrita. A aba "Adicionar Novo" e o "Gravar" não são tocados aqui.</para>
///
/// <para>Medido em 01/10/2026 (<c>Automais.SER/probe_profissional_saude.py</c>): filtro vazio
/// lista ~930 profissionais em 47 páginas de 20; é o universo com lotação no município, não o
/// Estado inteiro. A ordem não é alfabética e o índice da linha é absoluto
/// (<c>form0:listagem:{n}:…</c>) — é por ele que uma página é reconhecida como nova.</para>
/// </summary>
public interface ISerProfissionalLeitor
{
    Task<IReadOnlyList<SerProfissionalLinha>> LerTodosAsync(CancellationToken cancellationToken);
}

public sealed partial class SerProfissionalLeitor(
    ISerWebSessao sessao,
    ILogger<SerProfissionalLeitor> logger) : ISerProfissionalLeitor
{
    public const string CaminhoTela = "/ser/pages/cadastro/profissionalSaude/profissional-pesquisar.seam";

    private const string Form = "form0";
    private const string CampoNome = "form0:nome";
    private const string Scroller = "form0:sc1";

    /// <summary>Teto de páginas: 47 medidas; o dobro e um pouco é folga, não convite.</summary>
    private const int TetoDePaginas = 120;

    public async Task<IReadOnlyList<SerProfissionalLinha>> LerTodosAsync(CancellationToken cancellationToken)
    {
        var tela = await sessao.AbrirTelaAsync(CaminhoTela, cancellationToken);
        var docTela = SerWeb.SerHtmlParser.Documento(tela);

        // O botão é localizado pelo RÓTULO: o j_id é posicional e `form0:j_id36` é o Pesquisar
        // numa aba e o box de unidade na outra (medido em 01/10/2026).
        var botao = BotaoPesquisar(docTela)
            ?? throw new InvalidOperationException(
                "Botão Pesquisar não encontrado na tela de profissionais do SER (a SES-RJ mudou a tela?).");

        var pesquisa = await sessao.SubmeterFormAsync(
            tela, Form,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [botao] = "Pesquisar",
                [CampoNome] = string.Empty,
            },
            null, cancellationToken);

        var htmlPesquisa = Decodificar(pesquisa.Corpo);
        var porIndice = new SortedDictionary<int, SerProfissionalLinha>();
        foreach (var (i, l) in LerLinhas(htmlPesquisa)) porIndice[i] = l;

        var viewState = SerWeb.SerHtmlParser.ViewStateQualquer(htmlPesquisa);

        for (var pagina = 2; pagina <= TetoDePaginas; pagina++)
        {
            var resposta = await sessao.SubmeterFormAsync(
                htmlPesquisa, Form,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["AJAXREQUEST"] = "_viewRoot",
                    ["ajaxSingle"] = Scroller,
                    [Scroller] = pagina.ToString(CultureInfo.InvariantCulture),
                },
                viewState, cancellationToken);

            var html = Decodificar(resposta.Corpo);
            viewState = SerWeb.SerHtmlParser.ViewStateQualquer(html) ?? viewState;

            var novas = LerLinhas(html).Where(x => !porIndice.ContainsKey(x.Indice)).ToList();
            if (novas.Count == 0) break;
            foreach (var (i, l) in novas) porIndice[i] = l;
        }

        logger.LogInformation("SER/profissionais: {Qtd} linhas lidas da pesquisa.", porIndice.Count);
        return [.. porIndice.Values];
    }

    // ------------------------------------------------------------------ parsing (testável)

    /// <summary>
    /// O <c>name</c> do botão Pesquisar. Nesta tela o submit vem SÓ com <c>name</c>, sem
    /// <c>id</c> (<c>&lt;input type="submit" name="form0:j_id36" value="Pesquisar"&gt;</c>) — procurar
    /// pelo id derrubou a primeira importação em produção (01/10/2026).
    /// </summary>
    internal static string? BotaoPesquisar(IHtmlDocument doc) =>
        doc.QuerySelectorAll("input[type='submit']")
            .Select(e => (Nome: e.GetAttribute("name") ?? e.Id ?? string.Empty, Valor: e.GetAttribute("value")?.Trim()))
            .FirstOrDefault(e =>
                e.Nome.StartsWith("form0:", StringComparison.Ordinal)
                && string.Equals(e.Valor, "Pesquisar", StringComparison.Ordinal))
            .Nome is { Length: > 0 } nome ? nome : null;

    /// <summary>
    /// Linhas de <c>form0:listagem</c>: Ação | CPF | Documento | Tipo | Nome | Ativo. O índice
    /// absoluto sai do id da primeira célula.
    /// </summary>
    internal static IReadOnlyList<(int Indice, SerProfissionalLinha Linha)> LerLinhas(string html)
    {
        var doc = SerWeb.SerHtmlParser.Documento(html);
        var corpo = doc.GetElementById("form0:listagem:tb");
        if (corpo is null) return [];

        var saida = new List<(int, SerProfissionalLinha)>();
        foreach (var tr in corpo.Children.Where(c => c.LocalName == "tr"))
        {
            var tds = tr.Children.Where(c => c.LocalName == "td").ToList();
            if (tds.Count < 6) continue;

            var m = RegexIndice().Match(tds[0].Id ?? string.Empty);
            if (!m.Success) continue;

            var nome = Limpo(tds[4]);
            if (nome.Length == 0) continue;

            var ativo = tds[5].QuerySelectorAll("input[type='checkbox']")
                .Any(i => i.HasAttribute("checked"));

            saida.Add((int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                new SerProfissionalLinha(
                    NuloSeVazio(SoDigitos(Limpo(tds[1]))),
                    NuloSeVazio(Limpo(tds[2])),
                    NuloSeVazio(Limpo(tds[3])),
                    nome,
                    ativo)));
        }
        return saida;
    }

    /// <summary>
    /// O SER serve ISO-8859-1 e às vezes DECLARA UTF-8 (resposta A4J) — o header mente, e
    /// <see cref="RespostaSer.Texto"/> (sempre UTF-8) estragaria todo nome com acento. Decodifica
    /// pelo conteúdo: UTF-8 estrito; não sendo, ISO-8859-1.
    /// </summary>
    internal static string Decodificar(byte[] corpo)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(corpo);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(corpo);
        }
    }

    private static string Limpo(IElement el) =>
        RegexEspacos().Replace(el.TextContent, " ").Trim();

    private static string SoDigitos(string s) => new([.. s.Where(char.IsAsciiDigit)]);

    private static string? NuloSeVazio(string s) => s.Length == 0 ? null : s;

    [GeneratedRegex(@"^form0:listagem:(\d+):")]
    private static partial Regex RegexIndice();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacos();
}

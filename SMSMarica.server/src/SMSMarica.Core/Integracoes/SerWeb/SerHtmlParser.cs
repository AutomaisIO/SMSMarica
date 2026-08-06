using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace SMSMarica.Core.Integracoes.SerWeb;

/// <summary>
/// Leitura das telas do SER (JSF 1.2 + RichFaces 3.3.3). Ver <c>docs/ser.md</c>.
///
/// <para><b>Princípio de projeto: nunca casar por <c>j_id</c> fixo.</b> Os ids do JSF são
/// posicionais e mudam quando a SES-RJ recompila a página. Tudo aqui é localizado por rótulo
/// visível (<c>title="Pesquisar"</c>, texto "Historico da Solicitação", <c>&lt;label&gt;</c> do
/// mesmo <c>&lt;td&gt;</c>) e, quando o componente não é encontrado, o método devolve
/// <c>null</c> em vez de chutar — chutar faz o SER responder página vazia sem erro, e o chamador
/// acredita ter lido algo.</para>
/// </summary>
public static partial class SerHtmlParser
{
    public const string FormPesquisa = "form0";
    public const string TabelaGrade = "form0:listagem";
    public const string TabelaHistorico = "form0:historicoList";
    public const string Scroller = "form0:sc1";
    public const string CaixaMensagens = "form0:messages";

    private static readonly HtmlParser Parser = new();

    public static IHtmlDocument Documento(string html) => Parser.ParseDocument(html);

    // ------------------------------------------------------------------ JSF

    /// <summary>
    /// ViewState de DENTRO de um form específico. A home do SER tem vários forms e os
    /// ViewStates PODEM diferir; pegar o primeiro do documento faz o JSF restaurar a view errada
    /// e a ação não roda — HTTP 200, sem erro (ver docs/ser.md §3.2).
    /// </summary>
    public static string? ViewStateDoForm(IHtmlDocument doc, string formId)
    {
        var form = doc.GetElementById(formId) as IHtmlFormElement;
        var input = form?.QuerySelector("input[name='javax.faces.ViewState']") as IHtmlInputElement;
        return input?.Value;
    }

    /// <summary>ViewState de qualquer lugar do documento — usado só quando a resposta é parcial
    /// (paginação) e não traz form nenhum.</summary>
    public static string? ViewStateQualquer(string html)
    {
        var m = RegexViewState().Match(html);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Campos preenchidos de um form (hidden/texto/select), sem os botões de submit.</summary>
    public static Dictionary<string, string> CamposDoForm(IHtmlDocument doc, string formId)
    {
        var dados = new Dictionary<string, string>(StringComparer.Ordinal);
        if (doc.GetElementById(formId) is not IHtmlFormElement form) return dados;

        foreach (var el in form.QuerySelectorAll("input"))
        {
            if (el is not IHtmlInputElement input) continue;
            var nome = input.Name;
            if (string.IsNullOrEmpty(nome)) continue;
            var tipo = (input.Type ?? "text").ToLowerInvariant();
            if (tipo is "submit" or "button" or "image" or "reset") continue;
            if (tipo is "checkbox" or "radio" && !input.IsChecked) continue;
            dados[nome] = input.Value ?? string.Empty;
        }

        foreach (var el in form.QuerySelectorAll("select"))
        {
            if (el is not IHtmlSelectElement select || string.IsNullOrEmpty(select.Name)) continue;
            dados[select.Name] = select.Value ?? string.Empty;
        }

        return dados;
    }

    /// <summary>Action do form, para montar a URL do POST.</summary>
    public static string? ActionDoForm(IHtmlDocument doc, string formId) =>
        (doc.GetElementById(formId) as IHtmlFormElement)?.GetAttribute("action");

    /// <summary>
    /// Redirect do Ajax4JSF embutido no CORPO (<c>&lt;meta name="Location"&gt;</c>). O SER usa
    /// duas formas de redirect: header <c>Location</c> (escolha de módulo) e este meta (abrir o
    /// histórico, que responde 200 com ~268 bytes).
    /// </summary>
    public static string? RedirectNoCorpo(string html)
    {
        var m = RegexMetaLocation().Match(html);
        return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) : null;
    }

    /// <summary>Id do form de escolha de módulo da home (<c>action="/ser/home"</c>).</summary>
    public static string? FormDeModulo(string html)
    {
        var m = RegexFormModulo().Match(html);
        return m.Success ? m.Groups[1].Value : null;
    }

    // ------------------------------------------------------------------ pesquisa

    /// <summary>Id do botão Pesquisar, localizado pelo <c>title</c> (não pelo j_id).</summary>
    public static string? BotaoPesquisar(IHtmlDocument doc)
    {
        var el = doc.QuerySelectorAll("[title='Pesquisar']")
            .FirstOrDefault(e => (e.Id ?? string.Empty).StartsWith("form0:", StringComparison.Ordinal));
        return el?.Id;
    }

    /// <summary>Quantas páginas o <c>rich:datascroller</c> expõe. 5 = provável corte em 100.</summary>
    public static int PaginasNaResposta(IHtmlDocument doc)
    {
        var tab = doc.GetElementById($"{Scroller}_table");
        if (tab is null) return 0;

        var numeros = tab.QuerySelectorAll("td")
            .Select(td => td.TextContent.Trim())
            .Where(t => int.TryParse(t, out _))
            .Select(int.Parse)
            .ToList();

        return numeros.Count > 0 ? numeros.Max() : 1;
    }

    /// <summary>Lê a grade "Solicitações de Consulta ou Exame".</summary>
    public static IReadOnlyList<SerLinhaGrade> LerGrade(IHtmlDocument doc)
    {
        var tabela = doc.GetElementById(TabelaGrade);
        if (tabela is null) return [];

        var colunas = CabecalhoUtil(tabela);
        var linhas = new List<SerLinhaGrade>();

        foreach (var tr in tabela.QuerySelectorAll("tbody tr"))
        {
            var celulas = tr.QuerySelectorAll("td").Select(td => Texto(td)).ToList();
            if (celulas.Count < 5) continue;

            var mapa = Alinhar(colunas, celulas);
            var id = Valor(mapa, "ID");
            var paciente = Valor(mapa, "Paciente");
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(paciente)) continue;
            // Cabeçalho repetido dentro do tbody (o RichFaces faz isso) — ID não é numérico.
            if (!string.IsNullOrWhiteSpace(id) && !id.All(char.IsDigit)) continue;

            linhas.Add(new SerLinhaGrade
            {
                IdSer = id ?? string.Empty,
                Tipo = Valor(mapa, "Tipo"),
                Recurso = Valor(mapa, "Recurso"),
                DataSolicitacao = Valor(mapa, "Data da Solicitação"),
                Paciente = paciente,
                Idade = Valor(mapa, "Idade"),
                Cpf = Valor(mapa, "CPF"),
                Cns = Valor(mapa, "CNS"),
                Cid = Valor(mapa, "CID"),
                Solicitante = Valor(mapa, "Solicitante"),
                MunicipioSolicitante = Valor(mapa, "Município Solicitante"),
                AgendadoPara = Valor(mapa, "Agendado para"),
                Situacao = Valor(mapa, "Situação"),
            });
        }

        return linhas;
    }

    // ------------------------------------------------------------------ tela de export

    /// <summary>Mensagens que a tela devolve em <c>form0:messages</c> (avisos e erros do SER).</summary>
    public static IReadOnlyList<string> Mensagens(IHtmlDocument doc)
    {
        var caixa = doc.GetElementById(CaixaMensagens);
        if (caixa is null) return [];
        return caixa.QuerySelectorAll("li")
            .Select(Texto)
            .Where(t => t.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Aviso de corte da tela de Histórico, ou <c>null</c> quando o resultado veio inteiro.
    ///
    /// <para>Essa tela <b>avisa</b> quando trunca — <i>"Consulta muito ampla, retorno limitado em
    /// 500 resultados"</i> — ao contrário da tela de Solicitação, que corta em 100 calada. Usar o
    /// aviso como sinal é o que permite afirmar cobertura: sem aviso, o lote é completo. Inferir
    /// truncamento por contagem de linhas seria adivinhação (500 exatos podem ser o total real).</para>
    /// </summary>
    public static string? AvisoDeLimite(IHtmlDocument doc) =>
        Mensagens(doc).FirstOrDefault(m =>
            m.Contains("retorno limitado", StringComparison.OrdinalIgnoreCase)
            || m.Contains("muito ampla", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// <c>name</c> do <c>&lt;select&gt;</c> do form que oferece determinada <c>&lt;option&gt;</c>,
    /// ou <c>null</c>.
    ///
    /// <para>Serve para achar combos cujo id é opaco — o de Situação da tela de Histórico se chama
    /// <c>form0:j_id57</c>, e <c>j_id</c> é posicional: basta a SES-RJ recompilar a página para ele
    /// virar outro campo. Procurar pelo <i>conteúdo</i> (quem tem a opção <c>EM_FILA</c> é o combo
    /// de situação) é estável e ainda valida que o combo é mesmo o esperado.</para>
    /// </summary>
    public static string? SelectComOpcao(IHtmlDocument doc, string formId, string valorDeOpcao)
    {
        if (doc.GetElementById(formId) is not IHtmlFormElement form) return null;

        foreach (var el in form.QuerySelectorAll("select"))
        {
            if (el is not IHtmlSelectElement select || string.IsNullOrEmpty(select.Name)) continue;

            var tem = select.QuerySelectorAll("option")
                .Any(o => string.Equals(o.GetAttribute("value"), valorDeOpcao, StringComparison.Ordinal));

            if (tem) return select.Name;
        }

        return null;
    }

    /// <summary>Id do link "Exportar", localizado pelo <c>title</c> — nunca pelo <c>j_id</c>.</summary>
    public static string? BotaoExportar(IHtmlDocument doc)
    {
        var el = doc.QuerySelectorAll("a[title]")
            .FirstOrDefault(e =>
                (e.Id ?? string.Empty).StartsWith("form0:", StringComparison.Ordinal)
                && (e.GetAttribute("title") ?? string.Empty)
                    .Contains("Exportar", StringComparison.OrdinalIgnoreCase));
        return el?.Id;
    }

    // ------------------------------------------------------------------ menu Opções

    /// <summary>
    /// Id do item "Histórico da Solicitação" do menu Opções da linha, ou <c>null</c> quando a
    /// linha não oferece o item.
    ///
    /// <para><b>Sem fallback de propósito.</b> O menu MUDA por situação: "Em fila" tem
    /// Visualizar/Editar/Histórico/Cancelar/Registrar FollowUP; "Alta" não tem histórico nenhum.
    /// Chutar um <c>j_id</c> fixo faz o SER responder página vazia sem erro.</para>
    /// </summary>
    public static string? ItemHistorico(IHtmlDocument doc, int indiceNaPagina)
    {
        var prefixo = $"form0:listagem:{indiceNaPagina}:";
        return doc.QuerySelectorAll("a")
            .Where(a => (a.Id ?? string.Empty).StartsWith(prefixo, StringComparison.Ordinal))
            .FirstOrDefault(a => a.TextContent.Contains("Historico da Solicita", StringComparison.OrdinalIgnoreCase)
                              || a.TextContent.Contains("Histórico da Solicita", StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    /// <summary>Itens do menu Opções de uma linha (rótulo → id). Diagnóstico e mensagem de erro.</summary>
    public static IReadOnlyDictionary<string, string> ItensDeOpcoes(IHtmlDocument doc, int indiceNaPagina)
    {
        var prefixo = $"form0:listagem:{indiceNaPagina}:";
        var itens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in doc.QuerySelectorAll("a"))
        {
            if (!(a.Id ?? string.Empty).StartsWith(prefixo, StringComparison.Ordinal)) continue;
            var rotulo = a.TextContent.Trim();
            if (rotulo.Length > 0) itens[rotulo] = a.Id!;
        }
        return itens;
    }

    // ------------------------------------------------------------------ histórico

    /// <summary>Lê a tela de histórico: dados do paciente + trilha de eventos.</summary>
    public static SerHistorico LerHistorico(IHtmlDocument doc)
    {
        // Dados do paciente: inputs readonly com id volátil. Casamos pelo <label> do MESMO <td>
        // — assim a extração sobrevive a uma recompilação da página.
        var paciente = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var td in doc.QuerySelectorAll("td"))
        {
            var label = td.QuerySelector("label");
            var input = td.QuerySelector("input") as IHtmlInputElement;
            if (label is null || input is null) continue;

            var chave = Texto(label);
            var valor = (input.Value ?? string.Empty).Trim();
            if (chave.Length > 0 && valor.Length > 0) paciente[chave] = valor;
        }

        var eventos = new List<SerEventoLido>();
        var tabela = doc.GetElementById(TabelaHistorico);
        if (tabela is not null)
        {
            var colunas = CabecalhoUtil(tabela);
            foreach (var tr in tabela.QuerySelectorAll("tbody tr"))
            {
                var celulas = tr.QuerySelectorAll("td").Select(td => Texto(td)).ToList();
                if (celulas.Count == 0 || celulas.All(string.IsNullOrWhiteSpace)) continue;

                var mapa = Alinhar(colunas, celulas);
                var data = Valor(mapa, "Data");

                // O tbody REPETE o cabeçalho e traz linhas-tooltip só com a observação.
                // Só é evento de verdade quem tem data dd/MM/yyyy.
                if (string.IsNullOrWhiteSpace(data) || !RegexDataHora().IsMatch(data)) continue;

                eventos.Add(new SerEventoLido
                {
                    Data = data,
                    Evento = Valor(mapa, "Evento"),
                    EstadoAnterior = Valor(mapa, "Estado Anterior"),
                    EstadoAtual = Valor(mapa, "Estado Atual"),
                    CentralRegulacao = Valor(mapa, "Central regulação"),
                    UnidadeExecutora = Valor(mapa, "Unidade Executora"),
                    Usuario = Valor(mapa, "Usuário"),
                    LotacaoEvento = Valor(mapa, "Lotacao Evento"),
                    Ip = Valor(mapa, "IP"),
                    Observacao = Valor(mapa, "Observação"),
                });
            }
        }

        return new SerHistorico(paciente, eventos);
    }

    // ------------------------------------------------------------------ util

    /// <summary>Linha de cabeçalho com nomes de coluna (a grade tem antes uma linha de título
    /// mesclado com o nome da tabela).</summary>
    private static List<string> CabecalhoUtil(IElement tabela)
    {
        var melhor = new List<string>();
        foreach (var tr in tabela.QuerySelectorAll("thead tr"))
        {
            var ths = tr.QuerySelectorAll("th").Select(th => Texto(th)).ToList();
            if (ths.Count > melhor.Count) melhor = ths;
        }
        return melhor;
    }

    /// <summary>
    /// Casa nomes de coluna com células alinhando pelo FIM. A 1ª coluna do thead costuma ser o
    /// título mesclado da grade e as linhas de dados têm uma célula a menos.
    /// </summary>
    private static Dictionary<string, string> Alinhar(List<string> colunas, List<string> celulas)
    {
        var nomes = colunas.Count >= celulas.Count
            ? colunas.Skip(colunas.Count - celulas.Count).ToList()
            : colunas;

        var mapa = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < celulas.Count && i < nomes.Count; i++)
        {
            var nome = nomes[i];
            if (!string.IsNullOrWhiteSpace(nome) && !mapa.ContainsKey(nome)) mapa[nome] = celulas[i];
        }
        return mapa;
    }

    /// <summary>Busca tolerante: o SER põe o ícone de ordenação dentro do &lt;th&gt;, então o
    /// nome da coluna vem com sujeira ("ID ⇕").</summary>
    private static string? Valor(Dictionary<string, string> mapa, string coluna)
    {
        if (mapa.TryGetValue(coluna, out var direto)) return Limpar(direto);
        var chave = mapa.Keys.FirstOrDefault(k =>
            k.StartsWith(coluna, StringComparison.OrdinalIgnoreCase)
            || k.Contains(coluna, StringComparison.OrdinalIgnoreCase));
        return chave is null ? null : Limpar(mapa[chave]);
    }

    private static string? Limpar(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string Texto(IElement el) =>
        RegexEspacos().Replace(el.TextContent.Replace(' ', ' '), " ").Trim();

    [GeneratedRegex(@"name=""javax\.faces\.ViewState""[^>]*value=""([^""]*)""")]
    private static partial Regex RegexViewState();

    [GeneratedRegex(@"<meta name=""Location"" content=""([^""]+)""")]
    private static partial Regex RegexMetaLocation();

    [GeneratedRegex(@"<form id=""(j_id\d+)""[^>]*action=""/ser/home""")]
    private static partial Regex RegexFormModulo();

    [GeneratedRegex(@"^\d{2}/\d{2}/\d{4}")]
    private static partial Regex RegexDataHora();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacos();
}

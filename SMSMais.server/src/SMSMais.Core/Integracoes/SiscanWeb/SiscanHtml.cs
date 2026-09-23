using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace SMSMais.Core.Integracoes.SiscanWeb;

/// <summary>
/// Leitura das telas do SISCAN (JSF 1.2 + RichFaces 3.3.3 — o mesmo stack do SER).
///
/// <para><b>Princípio: nunca casar por <c>j_idNN</c> fixo.</b> Os ids do JSF são auto-gerados e
/// mudam conforme o caminho de renderização — medido no SISCAN em 07/08/2026: o checkbox
/// "Mamografia" é <c>frm:j_id47</c> vindo pelo menu e <b>não existe</b> na tela vinda por GET
/// direto. Um id fixo não só quebra em silêncio: pode acertar o campo errado. Tudo aqui é
/// localizado por rótulo visível, por <c>name</c> estável ou lido do próprio JavaScript da
/// página.</para>
///
/// <para>O protocolo completo está em <c>Automais.SISCAN/docs/FLUXO-NOVA-REQUISICAO.md</c>.</para>
/// </summary>
public static partial class SiscanHtml
{
    public const string FormPrincipal = "frm";
    public const string FormLogin = "formLogin";

    private static readonly HtmlParser Parser = new();

    public static IHtmlDocument Documento(string html) => Parser.ParseDocument(html);

    // ------------------------------------------------------------------ JSF

    /// <summary>
    /// Campos submissíveis do form, <b>como o navegador montaria</b>.
    ///
    /// <para>Os três descartes têm motivo medido: botão não é campo; campo <c>disabled</c> o
    /// navegador não posta (e no SISCAN vários são derivados pelo servidor — conselho, identidade
    /// do CADSUS —, então repostá-los é no melhor caso ruído); e radio/checkbox desmarcado não
    /// existe no POST.</para>
    /// </summary>
    public static Dictionary<string, string> CamposDoForm(IHtmlDocument doc, string formId)
    {
        var campos = new Dictionary<string, string>(StringComparer.Ordinal);
        if (doc.GetElementById(formId) is not IHtmlFormElement form) return campos;

        foreach (var el in form.QuerySelectorAll("input").OfType<IHtmlInputElement>())
        {
            var nome = el.Name;
            if (string.IsNullOrEmpty(nome)) continue;

            var tipo = (el.GetAttribute("type") ?? "text").ToLowerInvariant();
            if (tipo is "submit" or "reset" or "button" or "image") continue;
            if (el.HasAttribute("disabled")) continue;
            if (tipo is "checkbox" or "radio" && !el.HasAttribute("checked")) continue;

            campos[nome] = el.GetAttribute("value") ?? string.Empty;
        }

        foreach (var sel in form.QuerySelectorAll("select").OfType<IHtmlSelectElement>())
        {
            var nome = sel.Name;
            if (string.IsNullOrEmpty(nome) || sel.HasAttribute("disabled")) continue;

            var opcao = sel.QuerySelector("option[selected]") ?? sel.QuerySelector("option");
            campos[nome] = opcao?.GetAttribute("value") ?? string.Empty;
        }

        foreach (var area in form.QuerySelectorAll("textarea").OfType<IHtmlTextAreaElement>())
        {
            var nome = area.Name;
            if (string.IsNullOrEmpty(nome) || area.HasAttribute("disabled")) continue;
            campos[nome] = area.TextContent ?? string.Empty;
        }

        return campos;
    }

    /// <summary>URL de submissão REAL do form. Nunca chutar caminho constante.</summary>
    public static string? ActionDoForm(IHtmlDocument doc, string formId) =>
        (doc.GetElementById(formId) as IHtmlFormElement)?.GetAttribute("action");

    /// <summary>
    /// <c>javax.faces.ViewState</c> — inclusive de resposta A4J parcial.
    ///
    /// <para>Depois de um A4J o servidor emite um ViewState NOVO (bloco <c>ajax-view-state</c>).
    /// Postar com o anterior faz o JSF restaurar a view velha e <b>descartar em silêncio</b> o que
    /// o A4J tinha mudado: resposta sem erro, dado inalterado.</para>
    /// </summary>
    public static string? ViewState(IHtmlDocument doc)
    {
        var ajax = doc.GetElementById("ajax-view-state")
            ?.QuerySelector("input[name='javax.faces.ViewState']") as IHtmlInputElement;
        if (!string.IsNullOrEmpty(ajax?.Value)) return ajax.Value;

        var qualquer = doc.QuerySelector("input[name='javax.faces.ViewState']") as IHtmlInputElement;
        return qualquer?.Value;
    }

    /// <summary>
    /// Aplica a resposta A4J sobre o documento da tela, como o RichFaces faz no navegador.
    ///
    /// <para><b>A resposta de um A4J não é a tela.</b> É só o que está listado em
    /// <c>&lt;meta name="Ajax-Update-Ids"&gt;</c>. Quem lê a resposta crua não encontra
    /// <c>&lt;form id="frm"&gt;</c> e conclui, errado, que a ação não funcionou; pior, quem a
    /// reposta como se fosse a tela perde todos os campos fora da região atualizada.</para>
    /// </summary>
    public static IHtmlDocument AplicarA4J(IHtmlDocument baseDoc, IHtmlDocument parcial)
    {
        var ids = parcial.QuerySelectorAll("meta[name='Ajax-Update-Ids']")
            .SelectMany(m => (m.GetAttribute("content") ?? string.Empty).Split(','))
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .Distinct(StringComparer.Ordinal);

        foreach (var id in ids)
        {
            var novo = parcial.GetElementById(id);
            var antigo = baseDoc.GetElementById(id);
            if (novo is null || antigo is null) continue;

            antigo.Replace(baseDoc.Import(novo, deep: true));
        }

        var viewState = ViewState(parcial);
        if (!string.IsNullOrEmpty(viewState))
        {
            foreach (var el in baseDoc.QuerySelectorAll("input[name='javax.faces.ViewState']")
                         .OfType<IHtmlInputElement>())
            {
                el.Value = viewState;
            }
        }

        return baseDoc;
    }

    // ------------------------------------------------------------------ menu

    /// <summary>
    /// Id do item de menu com esse rótulo (sem o sufixo <c>:anchor</c>), ou null.
    ///
    /// <para>Navegar é CLICAR no menu. GET direto na URL da tela devolve HTTP 200 com a tela
    /// meio-inicializada — sem o checkbox de tipo de exame, com o combo de UF vazio —, e postar
    /// nela os ids colhidos da tela boa dá erro 500.</para>
    /// </summary>
    public static string? ItemDeMenu(IHtmlDocument doc, string rotulo)
    {
        foreach (var span in doc.QuerySelectorAll("span.rich-menu-item-label"))
        {
            var texto = span.TextContent?.Trim() ?? string.Empty;
            if (!string.Equals(texto, rotulo, StringComparison.OrdinalIgnoreCase)) continue;

            var id = span.GetAttribute("id") ?? string.Empty;
            return id.EndsWith(":anchor", StringComparison.Ordinal) ? id[..^":anchor".Length] : id;
        }

        return null;
    }

    /// <summary>Form que contém o item de menu — o par disparador vai nele, não em qualquer um.</summary>
    public static string? FormDoItemDeMenu(IHtmlDocument doc, string itemId) =>
        doc.GetElementById($"{itemId}:anchor")?.Closest("form")?.Id;

    // ------------------------------------------------------------------ A4J

    [GeneratedRegex(@"'parameters'\s*:\s*\{(.*?)\}", RegexOptions.Singleline)]
    private static partial Regex RegexParametros();

    [GeneratedRegex(@"'([^']+)'\s*:\s*'([^']*)'")]
    private static partial Regex RegexPar();

    /// <summary>
    /// Os parâmetros do <c>A4J.AJAX.Submit</c> de um handler (<c>onclick</c>/<c>onchange</c>/
    /// <c>onblur</c>). Lidos do próprio JavaScript da página porque o par disparador
    /// (<c>frm:j_idNN</c>) é auto-gerado — fixá-lo no código quebra na próxima recompilação deles.
    /// </summary>
    public static Dictionary<string, string> ParametrosA4J(string? javascript)
    {
        var parametros = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(javascript)) return parametros;

        var bloco = RegexParametros().Match(javascript);
        if (!bloco.Success) return parametros;

        foreach (Match par in RegexPar().Matches(bloco.Groups[1].Value))
        {
            parametros[par.Groups[1].Value] = par.Groups[2].Value;
        }

        return parametros;
    }

    /// <summary>Parâmetros A4J de um elemento, olhando os handlers na ordem em que existem.</summary>
    public static Dictionary<string, string> ParametrosA4JDoElemento(IElement? el)
    {
        foreach (var evento in new[] { "onclick", "onchange", "onblur" })
        {
            var parametros = ParametrosA4J(el?.GetAttribute(evento));
            if (parametros.Count > 0) return parametros;
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    // ------------------------------------------------------------------ campos

    /// <summary>Opções de um <c>&lt;select&gt;</c>, na ordem — (value, texto).</summary>
    public static List<(string Valor, string Texto)> Opcoes(IHtmlDocument doc, string name)
    {
        var sel = doc.QuerySelector($"select[name='{name}']");
        if (sel is null) return [];

        return sel.QuerySelectorAll("option")
            .Select(o => (o.GetAttribute("value") ?? string.Empty, (o.TextContent ?? string.Empty).Trim()))
            .ToList();
    }

    /// <summary>Valor de um campo pelo <c>name</c> (serve para conferir o que o servidor derivou).</summary>
    public static string? ValorDoCampo(IHtmlDocument doc, string name) =>
        (doc.QuerySelector($"[name='{name}']") as IHtmlInputElement)?.Value;

    /// <summary>Mensagens do JSF (erro/aviso). No SER é quem denuncia truncamento; aqui, validação.</summary>
    public static List<string> Mensagens(IHtmlDocument doc)
    {
        var fora = new List<string>();
        foreach (var classe in new[] { "rich-messages", "rich-message", "mensagem", "erro" })
        {
            foreach (var el in doc.QuerySelectorAll($"[class*='{classe}']"))
            {
                var texto = (el.TextContent ?? string.Empty).Trim();
                if (texto.Length is > 0 and < 300 && !fora.Contains(texto)) fora.Add(texto);
            }
        }

        return fora;
    }

    /// <summary>
    /// `name` de um campo resolvido pelo RÓTULO visível.
    ///
    /// <para>Obrigatório para os ids auto-gerados: o checkbox "Mamografia" é <c>frm:j_id47</c>
    /// vindo pelo menu, e o número muda conforme o caminho de renderização. Fixá-lo no código não
    /// só quebra em silêncio — pode acertar o campo errado, que é pior.</para>
    /// </summary>
    public static string? CampoPorRotulo(IHtmlDocument doc, string rotulo)
    {
        foreach (var label in doc.QuerySelectorAll("label").OfType<IHtmlLabelElement>())
        {
            var texto = (label.TextContent ?? string.Empty).Trim().TrimEnd(':').Trim();
            if (!string.Equals(texto, rotulo, StringComparison.OrdinalIgnoreCase)) continue;

            var alvo = label.HtmlFor;
            if (!string.IsNullOrEmpty(alvo) && doc.GetElementById(alvo) is { } el)
            {
                return el.GetAttribute("name");
            }

            // Padrão alternativo do SISCAN: o <label> ENVOLVE o input, sem `for`.
            var dentro = label.QuerySelector("input");
            if (dentro is not null) return dentro.GetAttribute("name");
        }

        return null;
    }

    /// <summary>
    /// `name` do grupo de radio/checkbox de uma pergunta, achado pela LEGENDA do
    /// <c>&lt;fieldset&gt;</c>.
    ///
    /// <para>É o que torna possível não fixar <c>frm:j_id91</c> e companhia no código. As
    /// perguntas da requisição são cada uma um fieldset com a pergunta na legenda — o texto é
    /// estável (é o que a paciente lê), o id não é. Comparação por trecho e sem acento, porque a
    /// legenda vem quebrada em várias linhas no HTML deles.</para>
    /// </summary>
    public static string? CampoPorLegenda(IHtmlDocument doc, string trechoDaLegenda)
    {
        var alvo = Simplificar(trechoDaLegenda);

        foreach (var fieldset in doc.QuerySelectorAll("fieldset"))
        {
            var legenda = fieldset.QuerySelector("legend");
            if (legenda is null) continue;
            if (!Simplificar(legenda.TextContent).Contains(alvo, StringComparison.Ordinal)) continue;

            var campo = fieldset.QuerySelector("input[type='radio'], input[type='checkbox']");
            var nome = campo?.GetAttribute("name");
            if (!string.IsNullOrEmpty(nome)) return nome;
        }

        return null;
    }

    /// <summary>Texto comparável: sem acento, sem espaço repetido, em caixa alta.</summary>
    private static string Simplificar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;

        var semAcento = new string(texto.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());

        return string.Join(' ', semAcento.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }

    /// <summary>Uma linha da grade de GERENCIAR EXAME.</summary>
    /// <param name="NumeroExame">Embutido no id das ações — <b>não</b> aparece como coluna.</param>
    /// <param name="Protocolo">A coluna "Protocolo" — número DIFERENTE do nº do exame.</param>
    public sealed record LinhaExame(
        string NumeroExame, string Protocolo, string Paciente, string Status,
        string Datas, string Unidade);

    [GeneratedRegex(@"frm:listaExamePaginada:(\d+):")]
    private static partial Regex RegexNumeroExame();

    /// <summary>
    /// Linhas da grade de resultados.
    ///
    /// <para>Cada <c>&lt;tr&gt;</c> tem tabelas aninhadas: sem <c>:scope &gt; tr</c> a contagem
    /// dobra. E o nº do exame só existe dentro do id das ações da linha — é por isso que reler a
    /// grade é a única forma de saber o número que o modal do Salvar não devolve.</para>
    /// </summary>
    public static List<LinhaExame> Grade(IHtmlDocument doc)
    {
        var tabela = doc.GetElementById("frm:listaExamePaginada");
        var corpo = doc.GetElementById("frm:listaExamePaginada:tb");
        if (tabela is null || corpo is null) return [];

        var cabecalho = tabela.QuerySelectorAll("thead th")
            .Select(th => (th.TextContent ?? string.Empty).Trim())
            .ToList();

        int Coluna(string titulo) =>
            cabecalho.FindIndex(c => c.Contains(titulo, StringComparison.OrdinalIgnoreCase));

        var iProtocolo = Coluna("Protocolo");
        var iPaciente = Coluna("Paciente");
        var iStatus = Coluna("Status");
        var iDatas = Coluna("Datas");
        var iUnidade = Coluna("Unidade Requisitante");

        var linhas = new List<LinhaExame>();
        foreach (var tr in corpo.QuerySelectorAll(":scope > tr"))
        {
            var celulas = tr.QuerySelectorAll(":scope > td")
                .Select(td => (td.TextContent ?? string.Empty).Trim())
                .ToList();

            var numero = tr.QuerySelectorAll("a")
                .Select(a => RegexNumeroExame().Match(a.GetAttribute("id") ?? string.Empty))
                .FirstOrDefault(m => m.Success)?.Groups[1].Value;
            if (string.IsNullOrEmpty(numero)) continue;

            string Em(int i) => i >= 0 && i < celulas.Count ? celulas[i] : string.Empty;

            linhas.Add(new LinhaExame(
                numero, Em(iProtocolo), Em(iPaciente), Em(iStatus), Em(iDatas), Em(iUnidade)));
        }

        return linhas;
    }

    /// <summary>
    /// Só os dígitos do protocolo. O modal do Salvar devolve <c>00000141043026</c> e a grade
    /// mostra <c>141043026</c> — é o mesmo número. Guardar dos dois jeitos faz a comparação falhar
    /// para sempre, e ninguém entende por quê.
    /// </summary>
    public static string NormalizarProtocolo(string? bruto) =>
        string.IsNullOrWhiteSpace(bruto)
            ? string.Empty
            : new string(bruto.Where(char.IsDigit).ToArray()).TrimStart('0');

    [GeneratedRegex(@"protocolo\s+gerado\s+é\s*:?\s*([0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexProtocoloGerado();

    /// <summary>
    /// O protocolo anunciado pelo modal do Salvar ("O número do protocolo gerado é 00000141043026"),
    /// já normalizado. Null quando o modal não trouxe número — o que significa que <b>não</b> houve
    /// criação, por mais que a resposta tenha voltado HTTP 200 sem erro.
    /// </summary>
    public static string? ProtocoloDoModal(IHtmlDocument doc)
    {
        var modal = doc.GetElementById("formModalExameSalvo");
        var texto = modal?.TextContent ?? doc.Body?.TextContent ?? string.Empty;
        var m = RegexProtocoloGerado().Match(texto);
        if (!m.Success) return null;

        var normalizado = NormalizarProtocolo(m.Groups[1].Value);
        return normalizado.Length == 0 ? null : normalizado;
    }

    /// <summary>Título da tela — é o <c>&lt;h1&gt;</c> que revela em que modo ela abriu.</summary>
    public static List<string> Titulos(IHtmlDocument doc) =>
        doc.QuerySelectorAll("h1")
            .Select(h => (h.TextContent ?? string.Empty).Trim())
            .Where(t => t.Length > 0)
            .ToList();
}

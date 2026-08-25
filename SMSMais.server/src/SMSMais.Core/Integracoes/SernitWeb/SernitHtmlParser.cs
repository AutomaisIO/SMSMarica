using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace SMSMais.Core.Integracoes.SernitWeb;

/// <summary>
/// O modal de observação (FollowUP) do SERNIT: form próprio, textarea, botão Gravar e o ViewState
/// de DENTRO desse form — usar o do <c>form0</c> faz o JSF restaurar a view errada.
/// </summary>
public sealed record SernitModalObservacao(
    string FormId, string CampoTexto, string BotaoGravar, string? ViewState);

/// <summary>Um <c>rich:suggestionbox</c> do SERNIT (CID da Hipótese) e seus ids voláteis, lidos do
/// init da página. O fetch manda <c>inputvalue</c>+<c>ajaxSingle</c> com <c>AJAXREQUEST=_viewRoot</c>.</summary>
public sealed record SernitSuggestionBox(string CampoTexto, string BoxId, string OnselectId)
{
    /// <summary>Hidden que carrega o índice da linha escolhida (preenchido só durante o onselect).</summary>
    public string CampoSelecao => $"{BoxId}_selection";
}

/// <summary>
/// Leitura das telas do SERNIT (SER de Niterói — JSF 1.2 + RichFaces 3.3.3 + Seam). Espelho do
/// <c>SerHtmlParser</c> do SER-RJ, adaptado às diferenças medidas no laboratório
/// (<c>Automais.SERNIT/docs/APRENDIZADOS.md</c>):
/// <list type="bullet">
/// <item>Pesquisar e Gravar são <c>&lt;input value="..."&gt;</c>, não <c>&lt;a title="..."&gt;</c>.</item>
/// <item>Não há Exportar nem autocomplete de Solicitante (a conta já é escopada a Maricá) — a
///   varredura é por PAGINAÇÃO, e o total real vem em <c>form0:msgErro</c>.</item>
/// </list>
///
/// <para><b>Princípio de projeto: nunca casar por <c>j_id</c> fixo.</b> Tudo é localizado por
/// rótulo/valor visível ou por conteúdo do <c>&lt;select&gt;</c>; quando não encontra, devolve
/// <c>null</c> em vez de chutar.</para>
/// </summary>
public static partial class SernitHtmlParser
{
    public const string FormPesquisa = "form0";
    public const string TabelaGrade = "form0:listagem";
    public const string TabelaHistorico = "form0:historicoList";
    public const string Scroller = "form0:sc1";
    public const string CaixaMensagens = "form0:msgErro";

    private static readonly HtmlParser Parser = new();

    public static IHtmlDocument Documento(string html) => Parser.ParseDocument(html);

    // ------------------------------------------------------------------ JSF

    /// <summary>ViewState de DENTRO de um form específico (a home tem vários; pegar o primeiro do
    /// documento faz o JSF restaurar a view errada).</summary>
    public static string? ViewStateDoForm(IHtmlDocument doc, string formId)
    {
        var form = doc.GetElementById(formId) as IHtmlFormElement;
        var input = form?.QuerySelector("input[name='javax.faces.ViewState']") as IHtmlInputElement;
        return input?.Value;
    }

    /// <summary>ViewState de qualquer lugar do documento — usado quando a resposta é parcial.</summary>
    public static string? ViewStateQualquer(string html)
    {
        var m = RegexViewState().Match(html);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Campos preenchidos de um form (hidden/texto/select), sem os botões de submit.
    /// <paramref name="comoNavegador"/> exclui os <c>disabled</c> (identidade do paciente) e inclui
    /// textareas — ligado só na escrita.</summary>
    public static Dictionary<string, string> CamposDoForm(
        IHtmlDocument doc, string formId, bool comoNavegador = false)
    {
        var dados = new Dictionary<string, string>(StringComparer.Ordinal);
        if (doc.GetElementById(formId) is not IHtmlFormElement form) return dados;

        foreach (var el in form.QuerySelectorAll("input"))
        {
            if (el is not IHtmlInputElement input) continue;
            var nome = input.Name;
            if (string.IsNullOrEmpty(nome)) continue;
            if (comoNavegador && input.HasAttribute("disabled")) continue;
            var tipo = (input.Type ?? "text").ToLowerInvariant();
            if (tipo is "submit" or "button" or "image" or "reset") continue;
            if (tipo is "checkbox" or "radio" && !input.IsChecked) continue;
            dados[nome] = input.Value ?? string.Empty;
        }

        foreach (var el in form.QuerySelectorAll("select"))
        {
            if (el is not IHtmlSelectElement select || string.IsNullOrEmpty(select.Name)) continue;
            if (comoNavegador && select.HasAttribute("disabled")) continue;
            dados[select.Name] = select.Value ?? string.Empty;
        }

        foreach (var el in form.QuerySelectorAll("textarea"))
        {
            if (el is not IHtmlTextAreaElement area || string.IsNullOrEmpty(area.Name)) continue;
            if (comoNavegador && area.HasAttribute("disabled")) continue;
            if (comoNavegador) dados[area.Name] = area.Value ?? string.Empty;
        }

        return dados;
    }

    /// <summary>Action do form, para montar a URL do POST (postar no action, nunca em constante).</summary>
    public static string? ActionDoForm(IHtmlDocument doc, string formId) =>
        (doc.GetElementById(formId) as IHtmlFormElement)?.GetAttribute("action");

    /// <summary>Redirect do Ajax4JSF embutido no CORPO (<c>&lt;meta name="Location"&gt;</c>).</summary>
    public static string? RedirectNoCorpo(string html)
    {
        var m = RegexMetaLocation().Match(html);
        return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) : null;
    }

    /// <summary>Id do form de escolha de módulo da home (<c>action="/ser/home"</c> — o caminho é o
    /// mesmo <c>/ser/</c> na instância de Niterói).</summary>
    public static string? FormDeModulo(string html)
    {
        var m = RegexFormModulo().Match(html);
        return m.Success ? m.Groups[1].Value : null;
    }

    // ------------------------------------------------------------------ login / pesquisa

    /// <summary>Botão Entrar do form de login — <c>&lt;input type="submit" value="Entrar"&gt;</c>,
    /// nome volátil (<c>login:j_id17</c> medido); resolver por valor.</summary>
    public static (string Nome, string Valor)? BotaoLogin(IHtmlDocument doc)
    {
        if (doc.GetElementById("login") is not IHtmlFormElement form) return null;
        var el = form.QuerySelectorAll("input")
            .OfType<IHtmlInputElement>()
            .FirstOrDefault(i =>
                string.Equals((i.Type ?? string.Empty), "submit", StringComparison.OrdinalIgnoreCase)
                || string.Equals((i.Value ?? string.Empty).Trim(), "Entrar", StringComparison.OrdinalIgnoreCase));
        return el is null || string.IsNullOrEmpty(el.Name) ? null : (el.Name!, el.Value ?? "Entrar");
    }

    /// <summary>Botão Pesquisar da tela de pesquisa — no SERNIT é
    /// <c>&lt;input type="button" value="Pesquisar"&gt;</c> (não <c>a[title]</c>). Resolver por valor.</summary>
    public static string? BotaoPesquisar(IHtmlDocument doc)
    {
        if (doc.GetElementById(FormPesquisa) is not IHtmlFormElement form) return null;
        var el = form.QuerySelectorAll("input, a, button")
            .FirstOrDefault(e => string.Equals(
                (e.GetAttribute("value") ?? e.GetAttribute("title") ?? e.TextContent).Trim(),
                "Pesquisar", StringComparison.OrdinalIgnoreCase));
        return el?.Id;
    }

    /// <summary>Quantas páginas o <c>rich:datascroller</c> expõe. 5 = teto de 100.</summary>
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

    /// <summary>
    /// <b>Total real</b> informado pela tela quando a grade é capada, lido de <c>form0:msgErro</c>
    /// ("...retorno limitado em 100 resultados... <b>Total de resultados encontrados: 415</b>").
    /// <c>null</c> quando a mensagem não veio (a janela cabe nas 100).
    ///
    /// <para>É este número — não a contagem de linhas — que autoriza afirmar quanto a janela tem.
    /// O "100 resultados" do aviso é o teto, não o total (docs SERNIT §5.1).</para>
    /// </summary>
    public static int? TotalDeResultados(IHtmlDocument doc)
    {
        var texto = MensagemDaTela(doc);
        var m = RegexTotalResultados().Match(texto);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }

    /// <summary>A grade foi capada em 100? (a tela avisa "limitado em 100").</summary>
    public static bool GradeCapada(IHtmlDocument doc) =>
        MensagemDaTela(doc).Contains("limitado em 100", StringComparison.OrdinalIgnoreCase);

    /// <summary>Lê a grade "Solicitações de Consulta ou Exame" (12 colunas no SERNIT — sem CPF/
    /// Solicitante/Município; as colunas ausentes voltam nulas).</summary>
    public static IReadOnlyList<SernitLinhaGrade> LerGrade(IHtmlDocument doc)
    {
        var tabela = doc.GetElementById(TabelaGrade);
        if (tabela is null) return [];

        var colunas = CabecalhoUtil(tabela);
        var linhas = new List<SernitLinhaGrade>();

        foreach (var tr in tabela.QuerySelectorAll("tbody tr"))
        {
            var celulas = tr.QuerySelectorAll("td").Select(Texto).ToList();
            if (celulas.Count < 5) continue;

            var mapa = Alinhar(colunas, celulas);
            var id = Valor(mapa, "ID");
            var paciente = Valor(mapa, "Paciente");
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(paciente)) continue;
            if (!string.IsNullOrWhiteSpace(id) && !id.All(char.IsDigit)) continue;

            linhas.Add(new SernitLinhaGrade
            {
                IdSernit = id ?? string.Empty,
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

    // ------------------------------------------------------------------ mensagens

    public static IReadOnlyList<string> Mensagens(IHtmlDocument doc)
    {
        var caixa = doc.GetElementById(CaixaMensagens);
        if (caixa is null) return [];
        var itens = caixa.QuerySelectorAll("li").Select(Texto).Where(t => t.Length > 0).ToList();
        // O msgErro do SERNIT nem sempre usa <li>; se não houver, vale o texto do próprio box.
        if (itens.Count == 0 && Texto(caixa) is { Length: > 0 } t) itens.Add(t);
        return itens;
    }

    /// <summary>O que a tela está exibindo, venha de <c>form0:msgErro</c> ou das caixas de mensagem
    /// de ação — ler só uma faz uma recusa parecer silenciosa.</summary>
    public static string MensagemDaTela(IHtmlDocument doc)
    {
        var partes = new List<string>();
        foreach (var id in new[] { CaixaMensagens, "form0:messages", "form0:divMensagens" })
        {
            if (doc.GetElementById(id) is { } caixa && Texto(caixa) is { Length: > 0 } t) partes.Add(t);
        }
        return string.Join(" | ", partes.Distinct(StringComparer.Ordinal));
    }

    /// <summary><c>name</c> do <c>&lt;select&gt;</c> que oferece determinada <c>&lt;option&gt;</c>
    /// (acha combos de id opaco pelo conteúdo — o de Situação é <c>form0:j_id54</c>).</summary>
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

    // ------------------------------------------------------------------ suggestionbox (CID)

    /// <summary>Localiza o <c>rich:suggestionbox</c> amarrado a um campo, lendo os ids do script
    /// <c>new RichFaces.Suggestion('form0','&lt;campo&gt;','&lt;box&gt;',{...})</c> da própria
    /// página (box `form0:j_id203` no CID, volátil). O onselect sai do handler.</summary>
    public static SernitSuggestionBox? SuggestionBoxDoCampo(string html, string campoTexto)
    {
        var box = Regex.Match(html,
            @"RichFaces\.Suggestion\(\s*'[^']*'\s*,\s*'" + Regex.Escape(campoTexto) + @"'\s*,\s*'([^']+)'");
        if (!box.Success) return null;

        var boxId = box.Groups[1].Value;
        var onselect = Regex.Match(html, @"'(" + Regex.Escape(boxId) + @":[A-Za-z_]\w*)'\s*:\s*'\1'");
        // No SERNIT o onselect é `A4J.AJAX.Submit('form0',...{'<box>:j_idNN':'<box>:j_idNN'}...)`.
        var onselectId = onselect.Success ? onselect.Groups[1].Value : $"{boxId}:onselect";
        return new SernitSuggestionBox(campoTexto, boxId, onselectId);
    }

    /// <summary>Linhas da tabela de sugestões (<c>&lt;box&gt;:suggest</c>) na ordem do DOM, cada uma
    /// como lista de células. <c>null</c> quando a resposta não trouxe a tabela.</summary>
    public static IReadOnlyList<IReadOnlyList<string>>? LinhasDeSugestao(IHtmlDocument doc, string boxId)
    {
        var tabela = doc.GetElementById($"{boxId}:suggest");
        if (tabela is null) return null;

        var corpo = tabela.QuerySelector("tbody") ?? tabela;
        return corpo.Children
            .Where(tr => tr.TagName.Equals("TR", StringComparison.OrdinalIgnoreCase))
            .Select(tr => (IReadOnlyList<string>)tr.QuerySelectorAll("td").Select(Texto).ToList())
            .ToList();
    }

    // ------------------------------------------------------------------ menu Opções

    /// <summary>Id do item "Histórico da Solicitação" do menu Opções da linha, ou <c>null</c>
    /// (situação Alta não oferece). Sem fallback — chutar j_id faz o SERNIT responder página vazia.</summary>
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

    /// <summary>Item "Registrar FollowUP" do menu Opções de uma linha.</summary>
    public static string? ItemFollowUp(IHtmlDocument doc, int indiceNaPagina)
    {
        var prefixo = $"form0:listagem:{indiceNaPagina}:";
        return doc.QuerySelectorAll("a")
            .Where(a => (a.Id ?? string.Empty).StartsWith(prefixo, StringComparison.Ordinal))
            .FirstOrDefault(a => a.TextContent
                .Replace("-", string.Empty)
                .Contains("followup", StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    /// <summary>Item "Editar" do menu Opções de uma linha (só EM_FILA/PENDENTE oferecem).</summary>
    public static string? ItemEditar(IHtmlDocument doc, int indiceNaPagina)
    {
        var prefixo = $"form0:listagem:{indiceNaPagina}:";
        return doc.QuerySelectorAll("a")
            .Where(a => (a.Id ?? string.Empty).StartsWith(prefixo, StringComparison.Ordinal))
            .FirstOrDefault(a => string.Equals(Texto(a), "Editar", StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    // ------------------------------------------------------------------ FollowUP

    /// <summary>O modal de observação do FollowUP: form próprio (não o <c>form0</c>), achado por ser
    /// o único com um <c>textarea</c> nomeado E um controle rotulado <i>Gravar</i>; ViewState de
    /// dentro dele.</summary>
    public static SernitModalObservacao? ModalDeObservacao(IHtmlDocument doc)
    {
        foreach (var form in doc.QuerySelectorAll("form").OfType<IHtmlFormElement>())
        {
            if (string.IsNullOrEmpty(form.Id)) continue;

            var textarea = form.QuerySelectorAll("textarea")
                .FirstOrDefault(t => !string.IsNullOrEmpty(t.GetAttribute("name")));
            if (textarea is null) continue;

            var gravar = form.QuerySelectorAll("a, input, button")
                .FirstOrDefault(e => string.Equals(
                    (e.GetAttribute("value") ?? e.GetAttribute("title") ?? e.TextContent).Trim(),
                    "Gravar", StringComparison.OrdinalIgnoreCase));
            if (gravar is null) continue;

            var nomeGravar = gravar.GetAttribute("name") ?? gravar.Id;
            if (string.IsNullOrEmpty(nomeGravar)) continue;

            return new SernitModalObservacao(
                form.Id!,
                textarea.GetAttribute("name")!,
                nomeGravar,
                (form.QuerySelector("input[name='javax.faces.ViewState']") as IHtmlInputElement)?.Value);
        }

        return null;
    }

    // ------------------------------------------------------------------ edição de contato

    /// <summary>
    /// Campo de um form localizado pelo RÓTULO visível (o <c>&lt;label&gt;</c> irmão), devolvendo
    /// <c>(name, valor)</c>. Os telefones do SERNIT têm id posicional/volátil
    /// (<c>form0:j_id157/j_id159</c>) — resolver pelo rótulo, nunca chumbar. Campo <c>disabled</c>
    /// é ignorado (o navegador não o envia; o SERNIT trava a identidade do paciente com ele).
    /// </summary>
    public static (string Nome, string Valor)? CampoPorRotulo(
        IHtmlDocument doc, string formId, string rotulo)
    {
        if (doc.GetElementById(formId) is not IHtmlFormElement form) return null;

        var alvo = NormalizarRotulo(rotulo);
        foreach (var el in form.QuerySelectorAll("input, textarea"))
        {
            var nome = el.GetAttribute("name");
            if (string.IsNullOrEmpty(nome) || el.HasAttribute("disabled")) continue;

            var tipo = (el.GetAttribute("type") ?? "text").ToLowerInvariant();
            if (tipo is "hidden" or "submit" or "button" or "image" or "reset") continue;

            var pai = el.ParentElement;
            if (pai is null) continue;
            if (pai.QuerySelectorAll("input, select, textarea").Length != 1) continue;

            var label = pai.Children
                .FirstOrDefault(f => string.Equals(f.TagName, "LABEL", StringComparison.OrdinalIgnoreCase));
            if (label is null) continue;
            if (!string.Equals(NormalizarRotulo(Texto(label)), alvo, StringComparison.Ordinal)) continue;

            var valor = el is IHtmlInputElement input ? input.Value : el.TextContent;
            return (nome, (valor ?? string.Empty).Trim());
        }

        return null;
    }

    private static string NormalizarRotulo(string texto) =>
        RegexEspacos()
            .Replace(texto.Replace("*", string.Empty).Replace(' ', ' '), " ")
            .Trim()
            .Trim(':')
            .Trim()
            .ToLowerInvariant();

    /// <summary>O controle <i>Gravar</i> DAQUELE form — no SERNIT é <c>&lt;input value="Gravar"&gt;</c>
    /// (não <c>a[title]</c>). Resolver por valor/título/texto.</summary>
    public static string? BotaoGravar(IHtmlDocument doc, string formId)
    {
        if (doc.GetElementById(formId) is not IHtmlFormElement form) return null;
        var el = form.QuerySelectorAll("input, a, button")
            .FirstOrDefault(e => string.Equals(
                (e.GetAttribute("value") ?? e.GetAttribute("title") ?? e.TextContent).Trim(),
                "Gravar", StringComparison.OrdinalIgnoreCase));
        return el?.Id;
    }

    /// <summary>A REGIÃO A4J que o próprio controle declara no <c>onclick</c>
    /// (<c>A4J.AJAX.Submit('form0', …)</c>). Vale para <c>&lt;a&gt;</c> e <c>&lt;input&gt;</c>.</summary>
    public static string? RegiaoDoBotao(string html, string idBotao)
    {
        var m = Regex.Match(
            html,
            "<(?:a|input|button)[^>]*id=\"" + Regex.Escape(idBotao) + "\"[^>]*>",
            RegexOptions.None,
            TimeSpan.FromSeconds(2));
        if (!m.Success) return null;

        var regiao = Regex.Match(m.Value, @"A4J\.AJAX\.Submit\(\s*'([^']+)'");
        return regiao.Success ? regiao.Groups[1].Value : null;
    }

    // ------------------------------------------------------------------ histórico

    /// <summary>Lê a tela de histórico: dados do paciente (label + input no MESMO td) + trilha de
    /// eventos (<c>form0:historicoList</c>). Estrutura idêntica à do SER-RJ (medido no SERNIT).</summary>
    public static SernitHistorico LerHistorico(IHtmlDocument doc)
    {
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

        var eventos = new List<SernitEventoLido>();
        var tabela = doc.GetElementById(TabelaHistorico);
        if (tabela is not null)
        {
            var colunas = CabecalhoUtil(tabela);
            foreach (var tr in tabela.QuerySelectorAll("tbody tr"))
            {
                var celulas = tr.QuerySelectorAll("td").Select(Texto).ToList();
                if (celulas.Count == 0 || celulas.All(string.IsNullOrWhiteSpace)) continue;

                var mapa = Alinhar(colunas, celulas);
                var data = Valor(mapa, "Data");
                if (string.IsNullOrWhiteSpace(data) || !RegexDataHora().IsMatch(data)) continue;

                eventos.Add(new SernitEventoLido
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

        return new SernitHistorico(paciente, eventos);
    }

    // ------------------------------------------------------------------ util

    private static List<string> CabecalhoUtil(IElement tabela)
    {
        var melhor = new List<string>();
        foreach (var tr in tabela.QuerySelectorAll("thead tr"))
        {
            var ths = tr.QuerySelectorAll("th").Select(Texto).ToList();
            if (ths.Count > melhor.Count) melhor = ths;
        }
        return melhor;
    }

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
        RegexEspacos().Replace(el.TextContent.Replace(' ', ' '), " ").Trim();

    [GeneratedRegex(@"name=""javax\.faces\.ViewState""[^>]*value=""([^""]*)""")]
    private static partial Regex RegexViewState();

    [GeneratedRegex(@"<meta name=""Location"" content=""([^""]+)""")]
    private static partial Regex RegexMetaLocation();

    [GeneratedRegex(@"<form id=""(j_id\d+)""[^>]*action=""/ser/home""")]
    private static partial Regex RegexFormModulo();

    [GeneratedRegex(@"Total de resultados encontrados:\s*(\d+)")]
    private static partial Regex RegexTotalResultados();

    [GeneratedRegex(@"^\d{2}/\d{2}/\d{4}")]
    private static partial Regex RegexDataHora();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacos();
}

using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Ser.Dtos;

namespace SMSMais.Core.Ser.Criacao;

/// <summary>
/// A aba <i>Editar</i> do SER em modo <b>criação</b>, dirigida como o navegador a dirige.
/// Porte para o servidor de <c>Automais.SER/probe_criar_solicitacao.py</c>, com o que o ensaio da
/// PR-20 (07/10/2026) confirmou contra o SER real.
///
/// <para><b>A página é mantida como o DOM do navegador.</b> Cada troca de combo, radio ou
/// autocomplete responde um PEDAÇO (A4J), com a lista dos ids re-renderizados no cabeçalho
/// <c>Ajax-Update-Ids</c>. O navegador troca esses elementos na página; aqui também. Na hora de
/// gravar, o formulário vai inteiro, do jeito que ficou — campo que sumiu (o texto livre da
/// unidade quando o radio vira "Sim") some do POST, e campo que apareceu (o bloco dinâmico do
/// recurso) entra. Juntar valores num dicionário, como a sonda fazia, deixaria passar campo
/// fantasma.</para>
///
/// <para><b>Nada aqui é identidade guardada.</b> Recurso, médico e CID são achados pelo NOME na
/// hora (Regulacao.Catalogo.IdentidadePorNome): o <c>value</c> dos combos do SER é posição.</para>
///
/// <para><b>Escrita só em dois pontos, os dois nomeados:</b> <see cref="AnexarAsync"/> (o arquivo
/// sobe para o Estado) e <see cref="GravarAsync"/>. Todo o resto passa pela trava de somente
/// leitura de <see cref="ISerWebSessao.SubmeterFormAsync"/>.</para>
/// </summary>
public sealed partial class SerCriacaoSolicitacao(
    ITransporteTelaCriacao transporte, PerfilTelaCriacao perfil, ILogger logger)
{
    /// <summary>O SER-RJ — o caso de origem; o SERNIT entra pelo construtor com perfil.</summary>
    public SerCriacaoSolicitacao(ISerWebSessao sessao, ILogger logger)
        : this(new TransporteSer(sessao), PerfilTelaCriacao.Ser, logger)
    {
    }

    private const string Form = SerHtmlParser.FormPesquisa;
    private const string AbrirAbaEditar = "form0:editar_server_submit";
    private const string RegiaoViewRoot = "_viewRoot";

    public const string CampoSisReg = "form0:comboSisReg";
    public const string CampoTipo = "form0:comboTipoRecurso";
    public const string CampoRecurso = "form0:comboRecurso";
    public const string CampoRecursoSugestao = "form0:suggRecurso";
    public const string PainelPaciente = "form0:painelDadosDoPaciente";
    public const string RadioMedicoIdentificado = "form0:booleanMedicoSolicitanteIdentificado_radio";
    public const string CampoMedico = "form0:medicoResp";
    public const string CampoRisco = "form0:classificacao_risco";
    public const string CampoHipotese = "form0:procedimento";
    public const string RadioUnidadeIdentificada = "form0:unidadeDeOrigemIdentificada_radio";
    public const string CampoUnidadeLivre = "form0:unidadeNaoIdentificada";
    public const string FormAnexar = "formAnexar";
    public const string CampoArquivo = "formAnexar:upload:file";
    public const string ComponenteUpload = "formAnexar:upload";

    private IHtmlDocument _pagina = SerHtmlParser.Documento(string.Empty);
    private string? _viewState;

    /// <summary>
    /// O que nós escolhemos/digitamos. Vai por cima do formulário no Gravar — o SER nem sempre
    /// re-renderiza o controle que mudou (o navegador guarda o valor no próprio DOM).
    /// </summary>
    private readonly Dictionary<string, string> _escolhas = new(StringComparer.Ordinal);

    /// <summary>A última resposta, crua — para diagnóstico quando algo não bate.</summary>
    public string UltimaResposta { get; private set; } = string.Empty;

    private string Html => _pagina.DocumentElement.OuterHtml;

    // ------------------------------------------------------------------ navegação

    public async Task AbrirAsync(CancellationToken ct)
    {
        var tela = await transporte.AbrirTelaAsync(perfil.CaminhoTela, ct);
        if (ControlePorRotulo(SerHtmlParser.Documento(tela), Form + ":", "Pesquisar") is null)
        {
            throw Falha($"O {perfil.Sistema} não devolveu a tela de Solicitação (sem botão Pesquisar). Sessão derrubada?");
        }

        var r = await transporte.SubmeterLeituraAsync(
            tela, Form,
            new Dictionary<string, string>(StringComparer.Ordinal) { [AbrirAbaEditar] = AbrirAbaEditar },
            SerHtmlParser.ViewStateQualquer(tela), ct);
        var html = await SeguirAsync(r, ct);

        if (!html.Contains(CampoTipo, StringComparison.Ordinal))
        {
            throw Falha($"A aba Editar do {perfil.Sistema} não abriu em modo criação (combo de Tipo ausente).");
        }

        _pagina = SerHtmlParser.Documento(html);
        _escolhas.Clear();
        _viewState = SerHtmlParser.ViewStateQualquer(html) ?? _viewState;
        UltimaResposta = html;
    }

    /// <summary>Dispara o <c>a4j:support</c> de um combo ou radio, como o <c>onchange</c> faria.</summary>
    public async Task TrocarAsync(string campo, string valor, CancellationToken ct)
    {
        var evento = EventoDoControle(Html, campo)
            ?? throw Falha($"Não achei o evento A4J do controle '{campo}' na tela do {perfil.Sistema}.");

        _escolhas[campo] = valor;
        await PostarLeituraAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [campo] = valor,
            ["AJAXREQUEST"] = RegiaoViewRoot,
            [evento] = evento,
            ["ajaxSingle"] = campo,
        }, ct);
    }

    /// <summary>
    /// As DUAS requisições do <c>rich:suggestionbox</c> (docs/ser.md §4.3): busca com o termo e
    /// <c>onselect</c> com o índice da linha. Texto solto no campo o SER descarta em silêncio.
    /// Devolve as células da linha escolhida; <c>null</c> se <paramref name="escolher"/> não
    /// escolheu nada (e então nada foi amarrado).
    /// </summary>
    public async Task<IReadOnlyList<string>?> AmarrarAsync(
        string campoTexto, string termo,
        Func<IReadOnlyList<IReadOnlyList<string>>, int?> escolher, CancellationToken ct)
    {
        var caixa = SerHtmlParser.SuggestionBoxDoCampo(Html, campoTexto)
            ?? throw Falha($"Não achei o autocomplete de '{campoTexto}' na tela do {perfil.Sistema}.");

        var busca = await transporte.SubmeterLeituraAsync(Html, Form, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AJAXREQUEST"] = RegiaoViewRoot,
            ["inputvalue"] = termo,
            [caixa.BoxId] = caixa.BoxId,
            ["ajaxSingle"] = caixa.BoxId,
        }, _viewState, ct);
        _viewState = SerHtmlParser.ViewStateQualquer(busca) ?? _viewState;

        var linhas = SerHtmlParser.LinhasDeSugestao(SerHtmlParser.Documento(busca), caixa.BoxId)
            ?? throw Falha($"O autocomplete de '{campoTexto}' não devolveu a tabela de sugestões.");
        if (escolher(linhas) is not int indice) return null;

        var escolha = await transporte.SubmeterLeituraAsync(Html, Form, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AJAXREQUEST"] = RegiaoViewRoot,
            [caixa.OnselectId] = caixa.OnselectId,
            ["ajaxSingle"] = caixa.BoxId,
            [$"{caixa.BoxId}_selection"] = indice.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }, _viewState, ct);

        // O efeito do onselect é invisível (Ajax-Update-Ids VAZIO): a escolha mora só na conversa
        // Seam. Sem o envelope A4J ela não aconteceu.
        if (!escolha.Contains("Ajax-Response", StringComparison.Ordinal)
            && !escolha.Contains("<form id=\"form0\"", StringComparison.Ordinal))
        {
            throw Falha($"O {perfil.Sistema} não confirmou a escolha no autocomplete de '{campoTexto}'.");
        }
        Absorver(escolha);

        // O texto da coluna oculta é o que o navegador escreve no campo — e o SER exige os dois.
        _escolhas[campoTexto] = linhas[indice][0];
        return linhas[indice];
    }

    public async Task PesquisarPacienteAsync(string cnsOuCpf, CancellationToken ct)
    {
        // <a title="Pesquisar"> no SER, <input value="Pesquisar"> no SERNIT — pelo rótulo.
        var botao = ControlePorRotulo(_pagina, Form + ":", "Pesquisar")
            ?? throw Falha("Não achei o botão Pesquisar do painel de paciente.");

        _escolhas[perfil.CampoCnsCpf] = cnsOuCpf;
        await PostarLeituraAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [perfil.CampoCnsCpf] = cnsOuCpf,
            ["AJAXREQUEST"] = RegiaoViewRoot,
            [botao] = botao,
            ["ajaxSingle"] = botao,
        }, ct);
    }

    /// <summary>Um texto digitado: só entra no Gravar, como no navegador.</summary>
    public void Digitar(string campo, string valor) => _escolhas[campo] = valor;

    // ------------------------------------------------------------------ leitura do estado

    public PerfilTelaCriacao Perfil => perfil;

    public List<SerOpcaoDto> Combo(string campo) => perfil.Combo(_pagina, campo);

    public List<SerCampoPacienteDto> Paciente() => perfil.Paciente(Html);

    public List<SerCampoDinamicoDto> CamposDinamicos() => perfil.CamposDinamicos(Html);

    /// <summary>O campo existe na página e o navegador o mandaria (não está travado).</summary>
    public bool CampoEditavel(string nome) =>
        _pagina.QuerySelector($"[name=\"{nome}\"]") is { } el && !el.HasAttribute("disabled") && !el.HasAttribute("readonly");

    /// <summary>Valor do campo como está na página (o que o servidor renderizou).</summary>
    public string? ValorNaPagina(string nome) => _pagina.QuerySelector($"[name=\"{nome}\"]")?.GetAttribute("value");

    public bool TemCampo(string nome) => _pagina.QuerySelector($"[name=\"{nome}\"]") is not null;

    public string Mensagem()
    {
        // O SERNIT também usa `form0:msgErro` (medido no lab) além das caixas do SER.
        var msg = SerHtmlParser.MensagemDaTela(_pagina);
        var erro = Espremer(_pagina.GetElementById("form0:msgErro")?.TextContent ?? string.Empty);
        return erro.Length == 0 || msg.Contains(erro, StringComparison.Ordinal) ? msg
            : msg.Length == 0 ? erro : $"{msg} | {erro}";
    }

    /// <summary>Conteúdo das células de <c>form0:anexoList</c> — para diagnóstico. Para conferir
    /// anexo use <see cref="AnexosNomes"/>.</summary>
    public List<string> AnexosListados()
    {
        var tabela = _pagina.GetElementById("form0:anexoList");
        var corpo = tabela?.QuerySelector("tbody");
        if (corpo is null) return [];
        return [.. corpo.QuerySelectorAll("tr")
            .SelectMany(tr => tr.QuerySelectorAll("td").Select(td => Espremer(td.TextContent)))
            .Where(t => t.Length > 0)];
    }

    /// <summary>
    /// A coluna <b>"Nome do Arquivo"</b> de cada linha de <c>form0:anexoList</c> (colunas: Data,
    /// Nome do Arquivo, Usuário, Ação). É por ela que se confere o anexo.
    ///
    /// <para><b>Contar linhas não serve</b> (08/10/2026): a PR-20 e a PR-22 foram gravadas com 2 linhas
    /// cada e o nome VAZIO — o "Anexar" do modal cria a linha mesmo quando o arquivo não chegou, e no
    /// SER o download sai "Null". Linha sem nome é anexo que não existe.</para>
    /// </summary>
    public List<string> AnexosNomes() => NomesDaGradeDeAnexos(_pagina);

    internal static List<string> NomesDaGradeDeAnexos(IHtmlDocument pagina)
    {
        var corpo = pagina.GetElementById("form0:anexoList")?.QuerySelector("tbody");
        if (corpo is null) return [];
        return [.. corpo.QuerySelectorAll("tr")
            .Select(tr => tr.QuerySelectorAll("td").ToList())
            .Where(tds => tds.Count >= 2)
            .Select(tds => Espremer(tds[1].TextContent))];
    }

    // ------------------------------------------------------------------ escrita

    /// <summary>
    /// Anexa UM arquivo como o navegador faz: "Anexar Arquivo" (abre o modal) → upload do
    /// <c>rich:fileUpload</c> → botão "Anexar" do modal. <b>ESCREVE</b>: o arquivo sobe para o
    /// servidor do Estado (na conversa do pedido; só vira anexo da solicitação no Gravar).
    /// </summary>
    public async Task AnexarAsync(string nome, string contentType, byte[] conteudo, CancellationToken ct)
    {
        // 1. "Anexar Arquivo" — submit A4J do form0 inteiro, como o clique faz.
        var abrir = ControlePorRotulo(_pagina, Form + ":", "Anexar Arquivo")
            ?? throw Falha($"Não achei o botão \"Anexar Arquivo\" na tela do {perfil.Sistema}.");
        // O resto do formulário já vai pela página; como "extras" só o que escolhemos/digitamos —
        // extra passa pela trava de somente-leitura, e o formulário inteiro ali seria ruído.
        var cliqueAbrir = new Dictionary<string, string>(_escolhas, StringComparer.Ordinal)
        {
            [abrir] = abrir,
            ["AJAXREQUEST"] = RegiaoDoControle(Html, abrir) ?? Form,
        };
        await PostarLeituraAsync(cliqueAbrir, ct);

        // 2. O upload: query do RichFaces 3.3 (lida do FileUpload do SER) + o arquivo.
        var uid = Guid.NewGuid().ToString("N");
        var upload = await transporte.EnviarArquivoAsync(
            Html, FormAnexar, CampoArquivo, nome, contentType, conteudo,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["_richfaces_upload_uid"] = uid,
                [ComponenteUpload] = ComponenteUpload,
                ["_richfaces_upload_file_indicator"] = "true",
                ["AJAXREQUEST"] = RegiaoViewRoot,
            },
            _viewState, $"anexar arquivo na nova solicitação ({nome})", ct);
        if (upload.Length > 0) Absorver(upload);

        // 3. "Anexar" do modal: passa o arquivo da área de upload para a lista do pedido.
        var confirmar = BotaoDoFormPorValor(FormAnexar, "Anexar")
            ?? throw Falha($"Não achei o botão \"Anexar\" do modal de anexos do {perfil.Sistema}.");
        var r = await transporte.SubmeterEscritaAsync(
            Html, FormAnexar, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [confirmar] = confirmar,
                ["AJAXREQUEST"] = FormAnexar,
                // O input de arquivo não viaja num POST comum — o navegador não o manda.
                [CampoArquivo] = SerHtmlParser.RemoverDoPost,
            },
            _viewState, $"confirmar anexo na nova solicitação ({nome})", ct);
        var confirmHtml = await SeguirAsync(r, ct);
        Absorver(confirmHtml);

        // DIAGNÓSTICO (envio ao SER): distingue "o SER não listou o anexo" de "a resposta A4J não
        // atualizou a grade que a gente relê". Loga o que o A4J mandou re-renderizar e como ficou o
        // `form0:anexoList` logo após confirmar — sem isso, a falha de conferência é indiagnosticável.
        var atualizados = string.Join(",", IdsAtualizados(SerHtmlParser.Documento(confirmHtml)));
        var listaAgora = AnexosListados();
        logger.LogInformation(
            "SER_ANEXO_DIAG [{Nome}] ({Sistema}): confirmado. A4J re-renderizou [{Ids}]; form0:anexoList "
            + "tem {N} célula(s): [{Lista}].",
            nome, perfil.Sistema, string.IsNullOrEmpty(atualizados) ? "(página inteira/sem meta)" : atualizados,
            listaAgora.Count, string.Join(" | ", listaAgora));
    }

    /// <summary>
    /// <b>GRAVA a solicitação no SER.</b> Manda o formulário inteiro como a página está, com as
    /// escolhas por cima, e o botão Gravar de <c>form0</c>. Devolve a resposta crua — quem chama
    /// lê o número e confere RELENDO do SER ("salvo com sucesso" não é prova).
    /// </summary>
    public async Task<string> GravarAsync(string operacao, CancellationToken ct)
    {
        // <a title="Gravar"> no SER, <input value="Gravar"> no SERNIT — sempre o de form0 (o modal
        // de cadastrar médico do SERNIT tem outro "Gravar").
        var gravar = ControlePorRotulo(_pagina, Form + ":", "Gravar")
            ?? throw Falha($"Não achei o botão Gravar da aba Editar do {perfil.Sistema}. Nada foi enviado.");

        var extras = Formulario(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [gravar] = gravar,
            // A região sai do onclick do botão: é ela que decide o que o A4J processa.
            ["AJAXREQUEST"] = RegiaoDoControle(Html, gravar) ?? Form,
        });

        var r = await transporte.SubmeterEscritaAsync(Html, Form, extras, _viewState, operacao, ct);
        var html = await SeguirAsync(r, ct);
        Absorver(html);
        return html;
    }

    /// <summary>
    /// O número que o sistema devolveu na mensagem do Gravar, se houver. Três dígitos ou mais: o
    /// SER-RJ numera com 7 (8407041), o SERNIT com 3 a 5 (9891, 43303).
    /// </summary>
    public static string? NumeroGerado(string html)
    {
        var mensagem = MensagemDaResposta(html);
        foreach (var padrao in new[] { RegexNumeroSolicitacao(), RegexNumeroNo() })
        {
            var m = padrao.Match(mensagem);
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    /// <summary>A mensagem que a resposta traz — as caixas do SER-RJ e o <c>form0:msgErro</c> do SERNIT.</summary>
    public static string MensagemDaResposta(string html)
    {
        var doc = SerHtmlParser.Documento(html);
        var msg = SerHtmlParser.MensagemDaTela(doc);
        var erro = Espremer(doc.GetElementById("form0:msgErro")?.TextContent ?? string.Empty);
        return erro.Length == 0 || msg.Contains(erro, StringComparison.Ordinal) ? msg
            : msg.Length == 0 ? erro : $"{msg} | {erro}";
    }

    [GeneratedRegex(@"[Ss]olicita[çc][ãa]o\D{0,40}?(\d{3,})")]
    private static partial Regex RegexNumeroSolicitacao();

    [GeneratedRegex(@"n[ºo°]\s*(\d{3,})", RegexOptions.IgnoreCase)]
    private static partial Regex RegexNumeroNo();

    // ------------------------------------------------------------------ interno

    /// <summary>
    /// Formulário inteiro (o que o navegador mandaria de form0 neste momento) + escolhas + extras.
    /// Campo travado (<c>disabled</c>) fica de fora, como no navegador — é assim que nome, CPF e
    /// CNS do paciente não voltam no POST.
    /// </summary>
    private Dictionary<string, string> Formulario(Dictionary<string, string> extras)
    {
        var campos = SerHtmlParser.CamposDoForm(_pagina, Form, comoNavegador: true);
        foreach (var (k, v) in _escolhas)
        {
            // Escolha de campo que a página não tem mais (ex.: texto livre da unidade depois de o
            // radio virar "Sim") não entra: o navegador também não mandaria.
            if (TemCampo(k) || k.EndsWith("InputDate", StringComparison.Ordinal)) campos[k] = v;
        }
        foreach (var (k, v) in extras) campos[k] = v;
        return campos;
    }

    private async Task PostarLeituraAsync(Dictionary<string, string> extras, CancellationToken ct)
    {
        var r = await transporte.SubmeterLeituraAsync(Html, Form, extras, _viewState, ct);
        Absorver(await SeguirAsync(r, ct));
    }

    private async Task<string> SeguirAsync(string html, CancellationToken ct) =>
        SerHtmlParser.RedirectNoCorpo(html) is { Length: > 0 } destino
            ? await transporte.AbrirTelaAsync(destino, ct)
            : html;

    /// <summary>
    /// Aplica a resposta na página como o A4J faz no navegador: página completa substitui tudo;
    /// pedaço troca só os elementos listados em <c>Ajax-Update-Ids</c>.
    /// </summary>
    private void Absorver(string html)
    {
        UltimaResposta = html;
        _viewState = SerHtmlParser.ViewStateQualquer(html) ?? _viewState;

        if (html.Contains("<form id=\"form0\"", StringComparison.Ordinal)
            && !html.Contains("Ajax-Update-Ids", StringComparison.Ordinal))
        {
            _pagina = SerHtmlParser.Documento(html);
            return;
        }

        var pedaco = SerHtmlParser.Documento(html);
        foreach (var id in IdsAtualizados(pedaco))
        {
            var novo = pedaco.GetElementById(id);
            var atual = _pagina.GetElementById(id);
            if (novo is null || atual is null) continue;
            // Como o A4J faz: o elemento inteiro dá lugar ao re-renderizado.
            atual.OuterHtml = novo.OuterHtml;
        }
    }

    internal static IEnumerable<string> IdsAtualizados(IHtmlDocument pedaco)
    {
        var meta = pedaco.QuerySelectorAll("meta")
            .FirstOrDefault(m => string.Equals(m.GetAttribute("name"), "Ajax-Update-Ids", StringComparison.OrdinalIgnoreCase));
        var conteudo = meta?.GetAttribute("content") ?? string.Empty;
        return conteudo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Id do <c>a4j:support</c> de um select OU de um radio (o <c>similarityGroupingId</c>).</summary>
    internal static string? EventoDoControle(string html, string nome)
    {
        foreach (var padrao in new[] { "<select[^>]*name=\"", "<input[^>]*name=\"" })
        {
            foreach (Match m in Regex.Matches(html, padrao + Regex.Escape(nome) + "\"[^>]*>"))
            {
                var g = Regex.Match(m.Value, @"'similarityGroupingId'\s*:\s*'([^']+)'");
                if (g.Success) return g.Groups[1].Value;
            }
        }
        return null;
    }

    /// <summary>
    /// Id do controle de <paramref name="prefixoId"/> com aquele rótulo: <c>&lt;a&gt;</c> pelo texto
    /// ou pelo <c>title</c> (o SER-RJ), <c>&lt;input type=button|submit&gt;</c> pelo <c>value</c> (o
    /// SERNIT). Nunca por id: <c>j_id</c> é posicional.
    /// </summary>
    internal static string? ControlePorRotulo(IHtmlDocument pagina, string prefixoId, string rotulo)
    {
        bool Casa(string? s) => Espremer(s ?? string.Empty).Equals(rotulo, StringComparison.OrdinalIgnoreCase);
        foreach (var el in pagina.QuerySelectorAll("a, input[type=button], input[type=submit]"))
        {
            var id = el.Id ?? el.GetAttribute("name") ?? string.Empty;
            if (!id.StartsWith(prefixoId, StringComparison.Ordinal)) continue;
            var ok = el.TagName.Equals("A", StringComparison.OrdinalIgnoreCase)
                ? Casa(el.TextContent) || Casa(el.GetAttribute("title"))
                : Casa(el.GetAttribute("value"));
            if (ok) return id;
        }
        return null;
    }

    /// <summary>A região A4J que o <c>onclick</c> do controle declara — <c>&lt;a&gt;</c> ou <c>&lt;input&gt;</c>.</summary>
    internal static string? RegiaoDoControle(string html, string id)
    {
        var m = Regex.Match(html, "<(a|input)[^>]*(id|name)=\"" + Regex.Escape(id) + "\"[^>]*>",
            RegexOptions.None, TimeSpan.FromSeconds(2));
        if (!m.Success) return null;
        var g = Regex.Match(m.Value, @"A4J\.AJAX\.Submit\(\s*'([^']+)'");
        return g.Success ? g.Groups[1].Value : null;
    }

    private string? BotaoDoFormPorValor(string formId, string valor) =>
        (_pagina.GetElementById(formId) as IElement)?.QuerySelectorAll("input[type=button], input[type=submit]")
            .FirstOrDefault(i => string.Equals((i.GetAttribute("value") ?? string.Empty).Trim(), valor, StringComparison.OrdinalIgnoreCase))
            ?.GetAttribute("name");

    private static string Espremer(string texto) =>
        string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private ValidacaoException Falha(string mensagem)
    {
        logger.LogWarning("{Sistema}/criação: {Mensagem}", perfil.Sistema, mensagem);
        return new ValidacaoException("ser.criacao", mensagem);
    }
}

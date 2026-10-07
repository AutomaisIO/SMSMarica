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
public sealed partial class SerCriacaoSolicitacao(ISerWebSessao sessao, ILogger logger)
{
    private const string Form = SerHtmlParser.FormPesquisa;
    private const string AbrirAbaEditar = "form0:editar_server_submit";
    private const string RegiaoViewRoot = "_viewRoot";

    public const string CampoSisReg = "form0:comboSisReg";
    public const string CampoTipo = "form0:comboTipoRecurso";
    public const string CampoRecurso = "form0:comboRecurso";
    public const string CampoRecursoSugestao = "form0:suggRecurso";
    public const string CampoCnsCpf = "form0:numeroCADSUS";
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
        var tela = await sessao.AbrirTelaAsync(SerNovaSolicitacaoService.CaminhoTela, ct);
        if (SerHtmlParser.BotaoPesquisar(SerHtmlParser.Documento(tela)) is null)
        {
            throw Falha("O SER não devolveu a tela de Solicitação (sem botão Pesquisar). Sessão derrubada?");
        }

        var r = await sessao.SubmeterFormAsync(
            tela, Form,
            new Dictionary<string, string>(StringComparer.Ordinal) { [AbrirAbaEditar] = AbrirAbaEditar },
            SerHtmlParser.ViewStateQualquer(tela), ct);
        var html = await SeguirAsync(r.Texto, ct);

        if (!html.Contains(CampoTipo, StringComparison.Ordinal))
        {
            throw Falha("A aba Editar do SER não abriu em modo criação (combo de Tipo ausente).");
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
            ?? throw Falha($"Não achei o evento A4J do controle '{campo}' na tela do SER.");

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
            ?? throw Falha($"Não achei o autocomplete de '{campoTexto}' na tela do SER.");

        var busca = await sessao.SubmeterFormAsync(Html, Form, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AJAXREQUEST"] = RegiaoViewRoot,
            ["inputvalue"] = termo,
            [caixa.BoxId] = caixa.BoxId,
            ["ajaxSingle"] = caixa.BoxId,
        }, _viewState, ct);
        _viewState = SerHtmlParser.ViewStateQualquer(busca.Texto) ?? _viewState;

        var linhas = SerHtmlParser.LinhasDeSugestao(SerHtmlParser.Documento(busca.Texto), caixa.BoxId)
            ?? throw Falha($"O autocomplete de '{campoTexto}' não devolveu a tabela de sugestões.");
        if (escolher(linhas) is not int indice) return null;

        var escolha = await sessao.SubmeterFormAsync(Html, Form, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AJAXREQUEST"] = RegiaoViewRoot,
            [caixa.OnselectId] = caixa.OnselectId,
            ["ajaxSingle"] = caixa.BoxId,
            [$"{caixa.BoxId}_selection"] = indice.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }, _viewState, ct);

        // O efeito do onselect é invisível (Ajax-Update-Ids VAZIO): a escolha mora só na conversa
        // Seam. Sem o envelope A4J ela não aconteceu.
        if (!escolha.Texto.Contains("Ajax-Response", StringComparison.Ordinal)
            && !escolha.Texto.Contains("<form id=\"form0\"", StringComparison.Ordinal))
        {
            throw Falha($"O SER não confirmou a escolha no autocomplete de '{campoTexto}'.");
        }
        Absorver(escolha.Texto);

        // O texto da coluna oculta é o que o navegador escreve no campo — e o SER exige os dois.
        _escolhas[campoTexto] = linhas[indice][0];
        return linhas[indice];
    }

    public async Task PesquisarPacienteAsync(string cnsOuCpf, CancellationToken ct)
    {
        var botao = SerNovaSolicitacaoService.BotaoPesquisarPaciente(Html)
            ?? throw Falha("Não achei o botão Pesquisar do painel de paciente.");

        _escolhas[CampoCnsCpf] = cnsOuCpf;
        await PostarLeituraAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CampoCnsCpf] = cnsOuCpf,
            ["AJAXREQUEST"] = RegiaoViewRoot,
            [botao] = botao,
            ["ajaxSingle"] = botao,
        }, ct);
    }

    /// <summary>Um texto digitado: só entra no Gravar, como no navegador.</summary>
    public void Digitar(string campo, string valor) => _escolhas[campo] = valor;

    // ------------------------------------------------------------------ leitura do estado

    public List<SerOpcaoDto> Combo(string campo) => SerNovaSolicitacaoService.Combo(_pagina, campo);

    public List<SerCampoPacienteDto> Paciente() => SerNovaSolicitacaoService.CamposDoPaciente(Html);

    public List<SerCampoDinamicoDto> CamposDinamicos() => SerNovaSolicitacaoService.CamposDinamicos(Html);

    public bool TemCampo(string nome) => _pagina.QuerySelector($"[name=\"{nome}\"]") is not null;

    public string Mensagem() => SerHtmlParser.MensagemDaTela(_pagina);

    /// <summary>Nomes de arquivo listados em <c>form0:anexoList</c>.</summary>
    public List<string> AnexosListados()
    {
        var tabela = _pagina.GetElementById("form0:anexoList");
        var corpo = tabela?.QuerySelector("tbody");
        if (corpo is null) return [];
        return [.. corpo.QuerySelectorAll("tr")
            .SelectMany(tr => tr.QuerySelectorAll("td").Select(td => Espremer(td.TextContent)))
            .Where(t => t.Length > 0)];
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
        var abrir = BotaoPorTexto(Form + ":", "Anexar Arquivo")
            ?? throw Falha("Não achei o botão \"Anexar Arquivo\" na tela do SER.");
        // O resto do formulário já vai pela página; como "extras" só o que escolhemos/digitamos —
        // extra passa pela trava de somente-leitura, e o formulário inteiro ali seria ruído.
        var cliqueAbrir = new Dictionary<string, string>(_escolhas, StringComparer.Ordinal)
        {
            [abrir] = abrir,
            ["AJAXREQUEST"] = SerHtmlParser.RegiaoDoBotao(Html, abrir) ?? Form,
        };
        await PostarLeituraAsync(cliqueAbrir, ct);

        // 2. O upload: query do RichFaces 3.3 (lida do FileUpload do SER) + o arquivo.
        var uid = Guid.NewGuid().ToString("N");
        var upload = await sessao.EnviarArquivoAsync(
            Html, FormAnexar, CampoArquivo, nome, contentType, conteudo,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["_richfaces_upload_uid"] = uid,
                [ComponenteUpload] = ComponenteUpload,
                ["_richfaces_upload_file_indicator"] = "true",
                ["AJAXREQUEST"] = RegiaoViewRoot,
            },
            _viewState, $"anexar arquivo na nova solicitação ({nome})", ct);
        if (upload.EhTexto) Absorver(upload.Texto);

        // 3. "Anexar" do modal: passa o arquivo da área de upload para a lista do pedido.
        var confirmar = BotaoDoFormPorValor(FormAnexar, "Anexar")
            ?? throw Falha("Não achei o botão \"Anexar\" do modal de anexos do SER.");
        var r = await sessao.SubmeterEscritaAsync(
            Html, FormAnexar, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [confirmar] = confirmar,
                ["AJAXREQUEST"] = FormAnexar,
                // O input de arquivo não viaja num POST comum — o navegador não o manda.
                [CampoArquivo] = SerHtmlParser.RemoverDoPost,
            },
            _viewState, $"confirmar anexo na nova solicitação ({nome})", ct);
        Absorver(await SeguirAsync(r.Texto, ct));
    }

    /// <summary>
    /// <b>GRAVA a solicitação no SER.</b> Manda o formulário inteiro como a página está, com as
    /// escolhas por cima, e o botão Gravar de <c>form0</c>. Devolve a resposta crua — quem chama
    /// lê o número e confere RELENDO do SER ("salvo com sucesso" não é prova).
    /// </summary>
    public async Task<string> GravarAsync(string operacao, CancellationToken ct)
    {
        var gravar = SerHtmlParser.BotaoGravar(_pagina, Form)
            ?? throw Falha("Não achei o botão Gravar da aba Editar do SER. Nada foi enviado.");

        var extras = Formulario(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [gravar] = gravar,
            // A região sai do onclick do botão: é ela que decide o que o A4J processa.
            ["AJAXREQUEST"] = SerHtmlParser.RegiaoDoBotao(Html, gravar) ?? Form,
        });

        var r = await sessao.SubmeterEscritaAsync(Html, Form, extras, _viewState, operacao, ct);
        var html = await SeguirAsync(r.Texto, ct);
        Absorver(html);
        return html;
    }

    /// <summary>O número que o SER devolveu na mensagem do Gravar (ou no campo de ID), se houver.</summary>
    public static string? NumeroGerado(string html)
    {
        var doc = SerHtmlParser.Documento(html);
        var mensagem = SerHtmlParser.MensagemDaTela(doc);
        foreach (var padrao in new[] { RegexNumeroSolicitacao(), RegexNumeroNo() })
        {
            var m = padrao.Match(mensagem);
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    [GeneratedRegex(@"[Ss]olicita[çc][ãa]o\D{0,40}?(\d{6,})")]
    private static partial Regex RegexNumeroSolicitacao();

    [GeneratedRegex(@"n[ºo°]\s*(\d{6,})", RegexOptions.IgnoreCase)]
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
        var r = await sessao.SubmeterFormAsync(Html, Form, extras, _viewState, ct);
        Absorver(await SeguirAsync(r.Texto, ct));
    }

    private async Task<string> SeguirAsync(string html, CancellationToken ct) =>
        SerHtmlParser.RedirectNoCorpo(html) is { Length: > 0 } destino
            ? await sessao.AbrirTelaAsync(destino, ct)
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

    private string? BotaoPorTexto(string prefixoId, string texto) =>
        _pagina.QuerySelectorAll("a")
            .FirstOrDefault(a => (a.Id ?? string.Empty).StartsWith(prefixoId, StringComparison.Ordinal)
                                 && Espremer(a.TextContent).Equals(texto, StringComparison.OrdinalIgnoreCase))
            ?.Id;

    private string? BotaoDoFormPorValor(string formId, string valor) =>
        (_pagina.GetElementById(formId) as IElement)?.QuerySelectorAll("input[type=button], input[type=submit]")
            .FirstOrDefault(i => string.Equals((i.GetAttribute("value") ?? string.Empty).Trim(), valor, StringComparison.OrdinalIgnoreCase))
            ?.GetAttribute("name");

    private static string Espremer(string texto) =>
        string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private ValidacaoException Falha(string mensagem)
    {
        logger.LogWarning("SER/criação: {Mensagem}", mensagem);
        return new ValidacaoException("ser.criacao", mensagem);
    }
}

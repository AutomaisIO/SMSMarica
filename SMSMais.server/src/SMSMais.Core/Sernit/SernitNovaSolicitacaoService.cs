using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Sernit.Dtos;

namespace SMSMais.Core.Sernit;

/// <summary>
/// Monta o formulário de <b>nova solicitação</b> lendo a aba <i>Editar</i> do SERNIT ao vivo.
/// Espelho do <c>SerNovaSolicitacaoService</c>, adaptado ao SERNIT (medido no lab, §5.6):
/// <list type="bullet">
/// <item><b>Sem "ambulatório estadual"</b> — não há esse gate; o recurso depende só do Tipo.</item>
/// <item>Troca de combo é A4J <c>AJAXREQUEST=_viewRoot</c> (não form0) + ajaxSingle + evento volátil.</item>
/// <item>Campos dinâmicos vivem em <c>&lt;span id="form0:campoDinamicoBox"&gt;</c> (fields
///   <c>form0:dinamico_id_N</c>, sem o wrapper <c>container_dinamico_id_N</c> do SER-RJ).</item>
/// <item>CID: suggestionbox <c>form0:procedimento</c> (box volátil <c>form0:j_id203</c>).</item>
/// </list>
///
/// <para><b>SOMENTE LEITURA</b> — só abre a aba e troca combos; o Gravar nunca é acionado.</para>
///
/// <para><b>Caveat de recon:</b> o parsing dos campos dinâmicos (em especial radios/labels) e a
/// pesquisa de paciente foram portados com a recon de 25/08 e devem ser exercitados pela
/// consulta-direta/lab contra o SERNIT real antes de confiar em produção.</para>
/// </summary>
public interface ISernitNovaSolicitacaoService
{
    Task<SernitFormularioNovaDto> ObterFormularioAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SernitOpcaoDto>> ListarRecursosAsync(string tipo, CancellationToken cancellationToken);

    Task<IReadOnlyList<SernitCampoDinamicoDto>> ObterCamposDinamicosAsync(
        string tipo, string recurso, CancellationToken cancellationToken);

    Task<SernitCidSugestoesDto> SugerirCidsAsync(
        string tipo, string recurso, string termo, CancellationToken cancellationToken);

    IAsyncEnumerable<SernitAssinaturaCidDto> MedirAssinaturasCidAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SernitCidDto>> CopiarListaCidAsync(
        string tipo, string recurso, CancellationToken cancellationToken);

    Task<SernitPacienteEncontradoDto> PesquisarPacienteAsync(string cnsOuCpf, CancellationToken cancellationToken);
}

public sealed partial class SernitNovaSolicitacaoService(
    ISernitWebSessao sessao,
    IMemoryCache cache,
    ILogger<SernitNovaSolicitacaoService> logger) : ISernitNovaSolicitacaoService
{
    public const string CaminhoTela =
        "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam";

    private const string AbrirAbaEditar = "form0:editar_server_submit";
    private const string CampoTipo = "form0:comboTipoRecurso";
    private const string CampoRecurso = "form0:comboRecurso";
    private const string CampoHipotese = "form0:procedimento";
    private const string CampoCns = "form0:numeroCNS";
    private const string CampoDinamicoBox = "form0:campoDinamicoBox";
    private const string RegiaoViewRoot = "_viewRoot";
    private const char SeparadorAssinatura = '|';
    private const int TetoDeSugestoes = 500;
    private static readonly string[] TermosDeSondagem = ["diab", "malig", "Z9"];

    private string _html = string.Empty;
    private string? _viewState;

    public async Task<SernitFormularioNovaDto> ObterFormularioAsync(CancellationToken cancellationToken)
    {
        var html = await AbrirEditarAsync(cancellationToken);
        var doc = SernitHtmlParser.Documento(html);

        return new SernitFormularioNovaDto(
            Combo(doc, CampoTipo),
            Combo(doc, "form0:classificacao_risco"),
            Combo(doc, "form0:medicoResp"),
            [.. CamposDinamicos(html)]);
    }

    public async Task<IReadOnlyList<SernitOpcaoDto>> ListarRecursosAsync(
        string tipo, CancellationToken cancellationToken)
    {
        var html = await PrepararAsync(tipo, cancellationToken);
        var recursos = Combo(SernitHtmlParser.Documento(html), CampoRecurso);

        if (recursos.Count == 0)
        {
            throw new ValidacaoException(
                "sernit.recursos_vazios",
                $"O SERNIT não devolveu recursos para o tipo {tipo}. O layout da aba mudou?");
        }

        logger.LogInformation("SERNIT/nova: {Qtd} recursos para {Tipo}.", recursos.Count, TipoParaOSernit(tipo));
        return recursos;
    }

    public async Task<IReadOnlyList<SernitCampoDinamicoDto>> ObterCamposDinamicosAsync(
        string tipo, string recurso, CancellationToken cancellationToken)
    {
        await PrepararAsync(tipo, cancellationToken);
        var html = await TrocarAsync(CampoRecurso, recurso, cancellationToken);
        return [.. CamposDinamicos(html)];
    }

    public async Task<SernitCidSugestoesDto> SugerirCidsAsync(
        string tipo, string recurso, string termo, CancellationToken cancellationToken)
    {
        var busca = (termo ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(recurso))
        {
            throw new ValidacaoException(
                "sernit.recurso_obrigatorio",
                "Escolha o recurso antes da hipótese: o SERNIT só lista os CID depois do recurso.");
        }

        var chave = $"sernit:cid:{tipo}:{recurso}:{busca.ToLowerInvariant()}";
        if (cache.TryGetValue(chave, out SernitCidSugestoesDto? guardado) && guardado is not null)
        {
            return guardado;
        }

        await PrepararAsync(tipo, cancellationToken);
        await TrocarAsync(CampoRecurso, recurso, cancellationToken);

        var itens = await BuscarNoSernitAsync(CaixaDaHipotese(), busca, cancellationToken);
        var saida = new SernitCidSugestoesDto(itens, itens.Count >= TetoDeSugestoes);
        cache.Set(chave, saida, TimeSpan.FromHours(6));
        return saida;
    }

    public async IAsyncEnumerable<SernitAssinaturaCidDto> MedirAssinaturasCidAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Sem ramo: uma aba, por tipo.
        await AbrirEditarAsync(cancellationToken);

        foreach (var tipo in (string[])["CONSULTA", "EXAME"])
        {
            var html = await TrocarAsync(CampoTipo, tipo, cancellationToken);
            var recursos = Combo(SernitHtmlParser.Documento(html), CampoRecurso);
            var caixa = CaixaDaHipotese();

            logger.LogInformation("SERNIT/cid: medindo {Qtd} recursos de {Tipo}.", recursos.Count, tipo);

            foreach (var r in recursos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await TrocarAsync(CampoRecurso, r.Valor, cancellationToken);

                var contagens = new int[TermosDeSondagem.Length];
                for (var i = 0; i < TermosDeSondagem.Length; i++)
                {
                    contagens[i] = (await BuscarNoSernitAsync(caixa, TermosDeSondagem[i], cancellationToken)).Count;
                }

                yield return new SernitAssinaturaCidDto(
                    tipo, r.Valor, string.Join(SeparadorAssinatura, contagens));
            }
        }
    }

    public async Task<IReadOnlyList<SernitCidDto>> CopiarListaCidAsync(
        string tipo, string recurso, CancellationToken cancellationToken)
    {
        await PrepararAsync(tipo, cancellationToken);
        await TrocarAsync(CampoRecurso, recurso, cancellationToken);
        var caixa = CaixaDaHipotese();

        var achados = new Dictionary<string, SernitCidDto>(StringComparer.Ordinal);
        var pendentes = new Queue<string>(
            from letra in "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
            from digito in "0123456789"
            select string.Concat(letra, digito));

        var buscas = 0;
        while (pendentes.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prefixo = pendentes.Dequeue();
            var linhas = await BuscarNoSernitAsync(caixa, prefixo, cancellationToken);
            buscas++;

            foreach (var c in linhas) achados[c.Codigo] = c;

            if (linhas.Count >= TetoDeSugestoes && prefixo.Length < 4)
            {
                foreach (var digito in "0123456789") pendentes.Enqueue(prefixo + digito);
            }
        }

        logger.LogInformation(
            "SERNIT/cid: recurso {Recurso} ({Tipo}) — {Qtd} CID em {Buscas} buscas.",
            recurso, tipo, achados.Count, buscas);

        return [.. achados.Values.OrderBy(c => c.Codigo, StringComparer.Ordinal)];
    }

    public async Task<SernitPacienteEncontradoDto> PesquisarPacienteAsync(
        string cnsOuCpf, CancellationToken cancellationToken)
    {
        var numero = new string([.. (cnsOuCpf ?? string.Empty).Where(char.IsDigit)]);
        if (numero.Length is not (11 or 15))
        {
            throw new ValidacaoException(
                "sernit.documento_invalido", "Informe um CNS (15 dígitos) ou um CPF (11 dígitos).");
        }

        await AbrirEditarAsync(cancellationToken);

        // Medido no lab (25/08): na aba Editar o botão Pesquisar do paciente é um
        // <input value="Pesquisar"> (id volátil form0:j_id60) — não um <a title>. É o único
        // "Pesquisar" da aba (a grade não está aqui), então BotaoPesquisar o resolve.
        var botao = SernitHtmlParser.BotaoPesquisar(SernitHtmlParser.Documento(_html))
            ?? throw new InvalidOperationException(
                "Não achei o botão Pesquisar do painel de paciente na aba Editar do SERNIT.");

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CampoCns] = numero,
            ["AJAXREQUEST"] = RegiaoViewRoot,
            [botao] = botao,
            ["ajaxSingle"] = botao,
        };

        var resposta = await sessao.SubmeterFormAsync(
            _html, SernitHtmlParser.FormPesquisa, extras, _viewState, cancellationToken);

        var html = resposta.Texto;
        if (SernitHtmlParser.RedirectNoCorpo(html) is { Length: > 0 } destino)
        {
            html = await sessao.AbrirTelaAsync(destino, cancellationToken);
        }

        _viewState = SernitHtmlParser.ViewStateQualquer(html) ?? _viewState;

        var campos = CamposDoPaciente(html);
        return new SernitPacienteEncontradoDto(campos.Count > 0, Avisos(html), campos);
    }

    // ------------------------------------------------------------------ navegação

    private async Task<string> AbrirEditarAsync(CancellationToken cancellationToken)
    {
        var tela = await sessao.AbrirTelaAsync(CaminhoTela, cancellationToken);
        var doc = SernitHtmlParser.Documento(tela);

        if (SernitHtmlParser.BotaoPesquisar(doc) is null)
        {
            throw new InvalidOperationException(
                "O SERNIT não devolveu a tela de Solicitação (sem botão Pesquisar). Sessão derrubada?");
        }

        var resposta = await sessao.SubmeterFormAsync(
            tela,
            SernitHtmlParser.FormPesquisa,
            new Dictionary<string, string>(StringComparer.Ordinal) { [AbrirAbaEditar] = AbrirAbaEditar },
            SernitHtmlParser.ViewStateQualquer(tela),
            cancellationToken);

        var html = resposta.Texto;
        if (SernitHtmlParser.RedirectNoCorpo(html) is { Length: > 0 } destino)
        {
            html = await sessao.AbrirTelaAsync(destino, cancellationToken);
        }

        if (!html.Contains(CampoTipo, StringComparison.Ordinal))
        {
            // DIAG temporário: que página o SERNIT devolveu quando o combo faltou?
            var b = html ?? string.Empty;
            logger.LogWarning(
                "SERNIT/diag AbrirEditar: combo ausente. len={Len} login={Login} aguarde={Ag} "
                + "temPesquisar={Pesq} temForm0={F0} temComboRecurso={CR} temCampoDin={CD} "
                + "temPainelPac={PP} temGrade={Grade} redirect={Red}. head={Head}",
                b.Length,
                b.Contains("login:password", StringComparison.Ordinal),
                b.Contains("AGUARDE", StringComparison.OrdinalIgnoreCase),
                b.Contains("Pesquisar", StringComparison.OrdinalIgnoreCase),
                b.Contains("id=\"form0\"", StringComparison.Ordinal) || b.Contains("name=\"form0\"", StringComparison.Ordinal),
                b.Contains("comboRecurso", StringComparison.Ordinal),
                b.Contains("campoDinamicoBox", StringComparison.Ordinal),
                b.Contains("painelDadosDoPaciente", StringComparison.Ordinal),
                b.Contains("form0:listagem", StringComparison.Ordinal) || b.Contains("datascroller", StringComparison.Ordinal),
                SernitHtmlParser.RedirectNoCorpo(b) ?? "(nenhum)",
                b.Length > 400 ? b[..400].Replace('\n', ' ').Replace('\r', ' ') : b);

            throw new InvalidOperationException(
                "A aba Editar do SERNIT não abriu (combo de Tipo ausente na resposta).");
        }

        _html = html;
        _viewState = SernitHtmlParser.ViewStateQualquer(html) ?? _viewState;
        return html;
    }

    /// <summary>Abre a aba e escolhe o Tipo (sem ramo ambulatório — o SERNIT não o tem).</summary>
    private async Task<string> PrepararAsync(string tipo, CancellationToken cancellationToken)
    {
        await AbrirEditarAsync(cancellationToken);
        return await TrocarAsync(CampoTipo, TipoParaOSernit(tipo), cancellationToken);
    }

    /// <summary>Dispara o onchange A4J de um combo — <c>_viewRoot</c> + ajaxSingle + evento volátil
    /// lido do <c>similarityGroupingId</c> (com <c>form0</c> o comboRecurso volta disabled+vazio).</summary>
    private async Task<string> TrocarAsync(string campo, string valor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_html)) await AbrirEditarAsync(cancellationToken);

        var evento = EventoDoCombo(_html, campo)
            ?? throw new InvalidOperationException(
                $"Não achei o onchange A4J do combo '{campo}' na aba Editar do SERNIT.");

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [campo] = valor,
            ["AJAXREQUEST"] = RegiaoViewRoot,
            [evento] = evento,
            ["ajaxSingle"] = campo,
        };

        var resposta = await sessao.SubmeterFormAsync(
            _html, SernitHtmlParser.FormPesquisa, extras, _viewState, cancellationToken);

        var html = resposta.Texto;
        if (SernitHtmlParser.RedirectNoCorpo(html) is { Length: > 0 } destino)
        {
            html = await sessao.AbrirTelaAsync(destino, cancellationToken);
        }

        _viewState = SernitHtmlParser.ViewStateQualquer(html) ?? _viewState;
        return html;
    }

    private SernitSuggestionBox CaixaDaHipotese() =>
        SernitHtmlParser.SuggestionBoxDoCampo(_html, CampoHipotese)
        ?? throw new InvalidOperationException(
            $"Não achei o script do autocomplete de CID ({CampoHipotese}) na aba Editar do SERNIT.");

    private async Task<List<SernitCidDto>> BuscarNoSernitAsync(
        SernitSuggestionBox caixa, string termo, CancellationToken cancellationToken)
    {
        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AJAXREQUEST"] = RegiaoViewRoot,
            ["inputvalue"] = termo,
            [caixa.BoxId] = caixa.BoxId,
            ["ajaxSingle"] = caixa.BoxId,
        };

        var resposta = await sessao.SubmeterFormAsync(
            _html, SernitHtmlParser.FormPesquisa, extras, _viewState, cancellationToken);
        _viewState = SernitHtmlParser.ViewStateQualquer(resposta.Texto) ?? _viewState;

        var linhas = SernitHtmlParser.LinhasDeSugestao(
                         SernitHtmlParser.Documento(resposta.Texto), caixa.BoxId)
            ?? throw new ValidacaoException(
                "sernit.autocomplete_sem_resposta",
                "O autocomplete de CID do SERNIT não devolveu a tabela de sugestões.");

        return CidsDaTabela(linhas);
    }

    // ------------------------------------------------------------------ leitura

    internal static string TipoParaOSernit(string tipo)
    {
        var limpo = (tipo ?? string.Empty).Trim();
        if (limpo.Equals("CONSULTA", StringComparison.OrdinalIgnoreCase)) return "CONSULTA";
        if (limpo.Equals("EXAME", StringComparison.OrdinalIgnoreCase)) return "EXAME";
        throw new ValidacaoException(
            "sernit.tipo_invalido",
            $"Tipo de recurso desconhecido para o SERNIT: \"{limpo}\". Só existem CONSULTA e EXAME.");
    }

    internal static string? EventoDoCombo(string html, string campo)
    {
        var sel = Regex.Match(html, "<select[^>]*name=\"" + Regex.Escape(campo) + "\"[^>]*>");
        if (!sel.Success) return null;
        var m = Regex.Match(sel.Value, @"'similarityGroupingId'\s*:\s*'([^']+)'");
        return m.Success ? m.Groups[1].Value : null;
    }

    internal static List<SernitOpcaoDto> Combo(IHtmlDocument doc, string nome)
    {
        var sel = doc.QuerySelector($"select[name=\"{nome}\"]");
        if (sel is null) return [];

        return [.. sel.QuerySelectorAll("option")
            .Select(o => new SernitOpcaoDto(o.GetAttribute("value") ?? string.Empty, Espremer(o.TextContent)))
            .Where(o => o.Valor.Length > 0
                        && !o.Valor.Contains("NoSelectionConverter", StringComparison.Ordinal)
                        && !string.Equals(o.Valor, "null", StringComparison.Ordinal))];
    }

    internal static IReadOnlyList<string> Avisos(string html)
    {
        var doc = SernitHtmlParser.Documento(html);
        foreach (var id in new[] { "form0:divMensagens", "form0:msgErro", "form0:messages" })
        {
            var div = doc.GetElementById(id);
            var texto = Espremer(div?.TextContent ?? string.Empty);
            if (texto.Length > 0) return [texto];
        }
        return [];
    }

    /// <summary>Cadastro do paciente que o SERNIT devolveu. Campo <c>disabled</c>/<c>readonly</c> não
    /// é editável (a identidade é travada). <b>Caveat: painel/campos não verificados no lab.</b></summary>
    internal static List<SernitCampoPacienteDto> CamposDoPaciente(string html)
    {
        var doc = SernitHtmlParser.Documento(html);
        var painel = doc.GetElementById("form0:painelDadosDoPaciente");
        if (painel is null) return [];

        var saida = new List<SernitCampoPacienteDto>();
        foreach (var el in painel.QuerySelectorAll("input, select, textarea"))
        {
            var nome = el.GetAttribute("name");
            if (string.IsNullOrEmpty(nome)) continue;

            var tipoHtml = (el.GetAttribute("type") ?? string.Empty).ToLowerInvariant();
            if (tipoHtml is "hidden" or "submit" or "button" or "image" or "reset") continue;

            var label = el.ParentElement?.QuerySelector("label");
            var obrigatorio = label?.QuerySelector("span.required") is not null;
            var rotulo = Espremer(label?.TextContent ?? string.Empty).Replace("*", string.Empty).Trim(' ', ':');

            var ehSelect = string.Equals(el.TagName, "select", StringComparison.OrdinalIgnoreCase);
            List<SernitOpcaoDto>? opcoes = null;
            string? valor;

            if (ehSelect)
            {
                opcoes = [.. el.QuerySelectorAll("option")
                    .Select(o => new SernitOpcaoDto(o.GetAttribute("value") ?? string.Empty, Espremer(o.TextContent)))
                    .Where(o => o.Valor.Length > 0)];
                valor = el.QuerySelectorAll("option")
                    .FirstOrDefault(o => o.HasAttribute("selected"))?.GetAttribute("value");
            }
            else
            {
                valor = el.GetAttribute("value");
            }

            saida.Add(new SernitCampoPacienteDto(
                nome, rotulo, string.IsNullOrWhiteSpace(valor) ? null : valor,
                ehSelect ? "select" : "text", obrigatorio,
                Editavel: !el.HasAttribute("disabled") && !el.HasAttribute("readonly"), opcoes));
        }
        return saida;
    }

    internal static List<SernitCidDto> CidsDaTabela(IReadOnlyList<IReadOnlyList<string>> linhas) =>
        [.. linhas
            .Where(l => l.Count >= 3 && l[1].Trim().Length > 0)
            .Select(l => new SernitCidDto(l[1].Trim(), l[2].Trim(), l[0].Trim()))];

    private static string Espremer(string texto) =>
        string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Os campos que o SERNIT acrescenta conforme o recurso, lidos de <c>form0:campoDinamicoBox</c>
    /// (fields <c>form0:dinamico_id_N</c>). VERIFICADO no lab (25/08): o box é <b>plano</b> — o
    /// rótulo do campo é o <c>&lt;label sem for&gt;</c> que o <b>precede</b>; o <c>*</c> vem num
    /// <c>&lt;label&gt;*&lt;/label&gt;</c> (ou <c>span.required</c>) entre o rótulo e o campo; e
    /// radio/checkbox têm um <c>input</c> por opção (mesmo <c>name</c>) com o texto em
    /// <c>&lt;label for="&lt;id&gt;"&gt;</c> (ex.: SIM/NÃO).
    /// </summary>
    internal static List<SernitCampoDinamicoDto> CamposDinamicos(string html)
    {
        var doc = SernitHtmlParser.Documento(html);
        var box = doc.GetElementById(CampoDinamicoBox);
        if (box is null) return [];

        // Mapa opção→rótulo (label[for]) do box inteiro — as opções de radio/checkbox saem daqui.
        var rotuloDaOpcao = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var l in box.QuerySelectorAll("label[for]"))
            rotuloDaOpcao[l.GetAttribute("for")!] = Espremer(l.TextContent);

        var saida = new List<SernitCampoDinamicoDto>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var rotulo = string.Empty;
        var obrigatorio = false;

        // Percorre o box em ORDEM DE DOCUMENTO acumulando o rótulo/obrigatório que precedem o campo.
        foreach (var el in box.QuerySelectorAll("label, span, input, select, textarea"))
        {
            var tag = el.TagName.ToUpperInvariant();

            if (tag == "LABEL")
            {
                if (el.HasAttribute("for")) continue;   // rótulo de OPÇÃO (SIM/NÃO), não do campo
                var txt = Espremer(el.TextContent);
                if (txt == "*") { obrigatorio = true; continue; }
                if (txt.Length == 0) continue;
                rotulo = txt.Trim(' ', ':');
                obrigatorio = false;
                continue;
            }

            if (tag == "SPAN")
            {
                var cls = el.GetAttribute("class") ?? string.Empty;
                if (cls.Contains("required", StringComparison.OrdinalIgnoreCase)
                    || Espremer(el.TextContent) == "*")
                {
                    obrigatorio = true;
                }
                continue;
            }

            // input/select/textarea
            var nome = el.GetAttribute("name");
            if (string.IsNullOrEmpty(nome) || !nome.Contains("dinamico_id_", StringComparison.Ordinal)) continue;
            if (!vistos.Add(nome)) continue;   // 2ª opção de um radio já contabilizada

            var numero = nome[(nome.LastIndexOf('_') + 1)..];
            var tipoHtml = (el.GetAttribute("type") ?? el.TagName).ToLowerInvariant();

            List<SernitOpcaoDto>? opcoes = null;
            string tipo;

            if (tipoHtml is "radio" or "checkbox")
            {
                tipo = tipoHtml;
                opcoes = [.. box.QuerySelectorAll($"input[name=\"{nome}\"]")
                    .Select(i => new SernitOpcaoDto(
                        i.GetAttribute("value") ?? string.Empty,
                        rotuloDaOpcao.TryGetValue(i.Id ?? string.Empty, out var r) && r.Length > 0
                            ? r : i.GetAttribute("value") ?? string.Empty))
                    .Where(o => o.Valor.Length > 0)
                    .DistinctBy(o => o.Valor, StringComparer.Ordinal)];
            }
            else if (nome.EndsWith("InputDate", StringComparison.Ordinal))
            {
                tipo = "date";
            }
            else if (string.Equals(el.TagName, "select", StringComparison.OrdinalIgnoreCase))
            {
                tipo = "select";
                opcoes = [.. el.QuerySelectorAll("option")
                    .Select(o => new SernitOpcaoDto(o.GetAttribute("value") ?? string.Empty, Espremer(o.TextContent)))
                    .Where(o => o.Valor.Length > 0)];
            }
            else if (string.Equals(el.TagName, "textarea", StringComparison.OrdinalIgnoreCase))
            {
                tipo = "textarea";
            }
            else
            {
                tipo = "text";
            }

            saida.Add(new SernitCampoDinamicoDto(numero, nome, rotulo, tipo, obrigatorio, opcoes));
            rotulo = string.Empty;
            obrigatorio = false;
        }

        return saida;
    }
}

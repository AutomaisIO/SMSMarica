using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.SerWeb;
using SMSMarica.Core.Ser.Dtos;

namespace SMSMarica.Core.Ser;

/// <summary>
/// Monta o formulário de <b>nova solicitação</b> lendo a aba <i>Editar</i> do SER ao vivo.
///
/// <para><b>Por que ao vivo e não catálogo fixo:</b> o formulário do SER tem duas partes — um
/// bloco fixo e um bloco DINÂMICO que muda conforme o Recurso. Medido em 08/08/2026: 203 recursos
/// produzem <b>21 formulários distintos</b>, com 163 campos únicos (oncologia pede peso, altura,
/// IMC e datas de biópsia; PET-CT pede grau histopatológico; cardiologia pede NYHA e grupo
/// sanguíneo). Chumbar isso viraria mentira no dia em que a SES-RJ mexer numa especialidade, e a
/// gente só descobriria pelo pedido recusado.</para>
///
/// <para><b>SOMENTE LEITURA — e aqui isso é crítico, porque é a tela de criação.</b> Este serviço
/// só abre a aba e troca combos, o que apenas re-renderiza a view. O botão <i>Gravar</i> nunca é
/// acionado, e a trava de <see cref="ISerWebSessao"/> recusa qualquer parâmetro com verbo de
/// escrita — a liberação nominal cobre só a troca de aba.</para>
/// </summary>
public interface ISerNovaSolicitacaoService
{
    /// <summary>Bloco fixo do formulário: combos e campos que valem para todo pedido.</summary>
    Task<SerFormularioNovaDto> ObterFormularioAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Catálogo de recursos de um tipo (CONSULTA/EXAME) <b>naquele ramo</b>.
    /// Medido em 10/08/2026: CONSULTA 120 no ramo Não e 151 no Sim; EXAME 83 e 64.
    /// </summary>
    Task<IReadOnlyList<SerOpcaoDto>> ListarRecursosAsync(
        string tipo, bool ambulatorioEstadual, CancellationToken cancellationToken);

    /// <summary>Campos dinâmicos que o SER exige para aquele recurso <b>naquele ramo</b>.</summary>
    Task<IReadOnlyList<SerCampoDinamicoDto>> ObterCamposDinamicosAsync(
        string tipo, string recurso, bool ambulatorioEstadual, CancellationToken cancellationToken);

    /// <summary>
    /// Sugestões de <b>CID</b> para a Hipótese — o autocomplete do próprio SER
    /// (<c>form0:procedimento</c>), medido em 20/08/2026.
    ///
    /// <para><b>A relação de CID depende do RECURSO, e por isso não dá para espelhá-la.</b> Sem
    /// recurso escolhido o SER responde "Nenhum CID encontrado" para qualquer termo — inclusive
    /// para o código exato. Com recurso escolhido, a lista muda de recurso para recurso: o
    /// "Ambulatório 1ª vez - Cirurgia Geral (Oncologia)" só aceita neoplasia (21 CID para
    /// "malig", <b>zero</b> para "diab" ou "hipert"), enquanto a cardiologia aceita 395 e 78.
    /// Copiar uma tabela de CID-10 para a nossa base ofereceria ao operador um código que o SER
    /// recusa na hora de gravar — e o SER recusa em silêncio.</para>
    ///
    /// <para><b>É consulta.</b> Só a primeira das duas requisições do <c>rich:suggestionbox</c>
    /// (o fetch, docs/ser.md §4.3): traz a tabela de sugestões e não amarra escolha nenhuma. A
    /// amarração — o <c>onselect</c> — pertence ao envio do pedido, que não existe ainda.</para>
    /// </summary>
    Task<SerCidSugestoesDto> SugerirCidsAsync(
        string tipo, string recurso, bool ambulatorioEstadual, string termo,
        CancellationToken cancellationToken);

    /// <summary>
    /// Pesquisa o paciente no SER por <b>CNS ou CPF</b> — o mesmo motor que a tela dele usa.
    ///
    /// <para>Resolve o que hoje só conseguimos pelo CADSUS via SISREG, que tem limitação de
    /// acesso: o SER devolve nome, <b>CPF</b>, CNS, nome social, nascimento, sexo, mãe, raça,
    /// endereço completo e três telefones.</para>
    ///
    /// <para><b>É consulta, não escrita.</b> O botão pesquisa e re-renderiza dois painéis; nada é
    /// gravado. A trava de somente-leitura segue valendo — "Pesquisar" não é verbo de escrita.</para>
    /// </summary>
    Task<SerPacienteEncontradoDto> PesquisarPacienteAsync(
        string cnsOuCpf, CancellationToken cancellationToken);
}

public sealed partial class SerNovaSolicitacaoService(
    ISerWebSessao sessao,
    IMemoryCache cache,
    ILogger<SerNovaSolicitacaoService> logger) : ISerNovaSolicitacaoService
{
    public const string CaminhoTela =
        "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam";

    /// <summary>Troca de aba: <c>_JSFFormSubmit</c>, POST comum sem <c>AJAXREQUEST</c>.</summary>
    private const string AbrirAbaEditar = "form0:editar_server_submit";

    /// <summary>"É AMBULATÓRIO ESTADUAL?" — o primeiro campo, e o que decide todo o resto.</summary>
    private const string CampoSisReg = "form0:comboSisReg";

    private const string CampoTipo = "form0:comboTipoRecurso";
    private const string CampoRecurso = "form0:comboRecurso";

    /// <summary>A <b>Hipótese</b>. O rótulo engana: não é campo de texto, é a caixa de
    /// autocomplete de CID (o input tem <c>alt="Digite o nome ou o código"</c>).</summary>
    private const string CampoHipotese = "form0:procedimento";

    /// <summary>Teto de linhas que o SER devolve numa busca de CID (medido em 20/08/2026: um
    /// termo de uma letra volta com exatamente 500).</summary>
    private const int TetoDeSugestoes = 500;

    /// <summary>Campo CNS/CPF do painel de paciente. Continua editável depois da pesquisa —
    /// é por ele que o número viaja no POST.</summary>
    private const string CampoCnsCpf = "form0:numeroCADSUS";

    /// <summary>Onde o SER renderiza o cadastro encontrado.</summary>
    private const string PainelPaciente = "form0:painelDadosDoPaciente";

    /// <summary>Container A4J default — o init dos combos não passa <c>containerId</c>.</summary>
    private const string RegiaoViewRoot = "_viewRoot";

    public async Task<SerFormularioNovaDto> ObterFormularioAsync(CancellationToken cancellationToken)
    {
        var html = await AbrirEditarAsync(cancellationToken);
        var doc = SerHtmlParser.Documento(html);

        return new SerFormularioNovaDto(
            Combo(doc, "form0:comboSisReg"),
            Combo(doc, CampoTipo),
            Combo(doc, "form0:classificacao_risco"),
            Combo(doc, "form0:medicoResp"),
            [.. CamposDinamicos(html)]);
    }

    public async Task<IReadOnlyList<SerOpcaoDto>> ListarRecursosAsync(
        string tipo, bool ambulatorioEstadual, CancellationToken cancellationToken)
    {
        var html = await PrepararAsync(ambulatorioEstadual, tipo, cancellationToken);
        var recursos = Combo(SerHtmlParser.Documento(html), CampoRecurso);

        if (recursos.Count == 0)
        {
            throw new ValidacaoException(
                "ser.recursos_vazios",
                $"O SER não devolveu recursos para o tipo {tipo}. O layout da aba mudou?");
        }

        logger.LogInformation(
            "SER/nova: {Qtd} recursos para {Tipo} (ambulatório estadual: {Ramo}).",
            recursos.Count, tipo, ambulatorioEstadual ? "Sim" : "Não");
        return recursos;
    }

    public async Task<IReadOnlyList<SerCampoDinamicoDto>> ObterCamposDinamicosAsync(
        string tipo, string recurso, bool ambulatorioEstadual, CancellationToken cancellationToken)
    {
        await PrepararAsync(ambulatorioEstadual, tipo, cancellationToken);
        var html = await TrocarAsync(CampoRecurso, recurso, cancellationToken);
        return [.. CamposDinamicos(html)];
    }

    public async Task<SerCidSugestoesDto> SugerirCidsAsync(
        string tipo, string recurso, bool ambulatorioEstadual, string termo,
        CancellationToken cancellationToken)
    {
        var busca = (termo ?? string.Empty).Trim();
        if (busca.Length < 2)
        {
            throw new ValidacaoException(
                "ser.termo_curto",
                "Digite pelo menos 2 caracteres do código ou do nome do CID.");
        }

        if (string.IsNullOrWhiteSpace(recurso))
        {
            throw new ValidacaoException(
                "ser.recurso_obrigatorio",
                "Escolha o recurso antes da hipótese: o SER só lista os CID depois de saber qual "
                + "é o procedimento, e a lista muda conforme ele.");
        }

        // Cada busca custa ao SER uma conversa Seam inteira (abrir a aba, o ramo, o tipo e o
        // recurso) e a sessão é ÚNICA e serializada — a mesma que a varredura usa. Guardar a
        // resposta por termo é o que impede a digitação de um operador de enfileirar o motor.
        // A relação de CID de um recurso não muda no meio do expediente.
        var chave = $"ser:cid:{(ambulatorioEstadual ? "amb" : "nao")}:{tipo}:{recurso}"
                    + $":{busca.ToLowerInvariant()}";
        if (cache.TryGetValue(chave, out SerCidSugestoesDto? guardado) && guardado is not null)
        {
            return guardado;
        }

        await PrepararAsync(ambulatorioEstadual, tipo, cancellationToken);
        await TrocarAsync(CampoRecurso, recurso, cancellationToken);

        // O script `new RichFaces.Suggestion(...)` está na página COMPLETA da aba — os fragmentos
        // A4J das trocas de combo não o trazem. `_html` é ela.
        var caixa = SerHtmlParser.SuggestionBoxDoCampo(_html, CampoHipotese)
            ?? throw new InvalidOperationException(
                $"Não achei o script do autocomplete de CID ({CampoHipotese}) na aba Editar do "
                + "SER. O layout mudou?");

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AJAXREQUEST"] = RegiaoViewRoot,
            // `inputvalue` é o default do RichFaces para o texto digitado; o init do componente
            // não o sobrescreve.
            ["inputvalue"] = busca,
            [caixa.BoxId] = caixa.BoxId,
            ["ajaxSingle"] = caixa.BoxId,
        };

        var resposta = await sessao.SubmeterFormAsync(
            _html, SerHtmlParser.FormPesquisa, extras, _viewState, cancellationToken);

        _viewState = SerHtmlParser.ViewStateQualquer(resposta.Texto) ?? _viewState;

        var linhas = SerHtmlParser.LinhasDeSugestao(
                         SerHtmlParser.Documento(resposta.Texto), caixa.BoxId)
            ?? throw new ValidacaoException(
                "ser.autocomplete_sem_resposta",
                "O autocomplete de CID do SER não devolveu a tabela de sugestões — o protocolo "
                + "mudou, ou a sessão caiu. Nenhum CID foi listado.");

        var itens = CidsDaTabela(linhas);
        var saida = new SerCidSugestoesDto(itens, itens.Count >= TetoDeSugestoes);

        cache.Set(chave, saida, TimeSpan.FromHours(6));

        logger.LogDebug(
            "SER/cid: \"{Termo}\" no recurso {Recurso} ({Tipo}, ambulatório estadual {Ramo}) "
            + "devolveu {Qtd} CID{Corte}.",
            busca, recurso, tipo, ambulatorioEstadual ? "Sim" : "Não", itens.Count,
            saida.Truncado ? " (no teto do SER)" : string.Empty);

        return saida;
    }

    public async Task<SerPacienteEncontradoDto> PesquisarPacienteAsync(
        string cnsOuCpf, CancellationToken cancellationToken)
    {
        var numero = new string([.. (cnsOuCpf ?? string.Empty).Where(char.IsDigit)]);
        if (numero.Length is not (11 or 15))
        {
            throw new ValidacaoException(
                "ser.documento_invalido",
                "Informe um CNS (15 dígitos) ou um CPF (11 dígitos).");
        }

        await AbrirEditarAsync(cancellationToken);

        // Id do botão lido da PÁGINA pelo title, nunca chumbado: `j_id` é posicional e muda
        // quando a SES-RJ recompila — foi assim que o id do combo de recurso já mudou.
        var botao = BotaoPesquisarPaciente(_html)
            ?? throw new InvalidOperationException(
                "Não achei o botão Pesquisar do painel de paciente na aba Editar do SER.");

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CampoCnsCpf] = numero,
            ["AJAXREQUEST"] = RegiaoViewRoot,
            [botao] = botao,
            ["ajaxSingle"] = botao,
        };

        var resposta = await sessao.SubmeterFormAsync(
            _html, SerHtmlParser.FormPesquisa, extras, _viewState, cancellationToken);

        var html = resposta.Texto;
        if (SerHtmlParser.RedirectNoCorpo(html) is { Length: > 0 } destino)
        {
            html = await sessao.AbrirTelaAsync(destino, cancellationToken);
        }

        _viewState = SerHtmlParser.ViewStateQualquer(html) ?? _viewState;

        var campos = CamposDoPaciente(html);
        logger.LogInformation(
            "SER/paciente: pesquisa por {Doc} devolveu {Qtd} campo(s).",
            numero.Length == 11 ? "CPF" : "CNS", campos.Count);

        return new SerPacienteEncontradoDto(campos.Count > 0, Avisos(html), campos);
    }

    // ------------------------------------------------------------------ navegação

    private string _html = string.Empty;
    private string? _viewState;

    /// <summary>Abre a aba Editar. GET novo sempre — a mesma regra do resto do motor.</summary>
    private async Task<string> AbrirEditarAsync(CancellationToken cancellationToken)
    {
        var tela = await sessao.AbrirTelaAsync(CaminhoTela, cancellationToken);
        var doc = SerHtmlParser.Documento(tela);

        if (SerHtmlParser.BotaoPesquisar(doc) is null)
        {
            throw new InvalidOperationException(
                "O SER não devolveu a tela de Solicitação (sem botão Pesquisar). Sessão derrubada?");
        }

        var resposta = await sessao.SubmeterFormAsync(
            tela,
            SerHtmlParser.FormPesquisa,
            new Dictionary<string, string>(StringComparer.Ordinal) { [AbrirAbaEditar] = AbrirAbaEditar },
            SerHtmlParser.ViewStateQualquer(tela),
            cancellationToken);

        var html = resposta.Texto;
        if (SerHtmlParser.RedirectNoCorpo(html) is { Length: > 0 } destino)
        {
            html = await sessao.AbrirTelaAsync(destino, cancellationToken);
        }

        // A aba Editar tem o combo de Tipo; sem ele, não abriu.
        if (!html.Contains(CampoTipo, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A aba Editar do SER não abriu (combo de Tipo ausente na resposta).");
        }

        _html = html;
        _viewState = SerHtmlParser.ViewStateQualquer(html) ?? _viewState;
        return html;
    }

    /// <summary>
    /// Põe o formulário no estado em que os combos seguintes fazem sentido: <b>ramo primeiro,
    /// tipo depois</b>.
    ///
    /// <para><b>A ordem não é estética.</b> "É AMBULATÓRIO ESTADUAL?" decide QUAIS recursos o SER
    /// lista, e cada troca é uma conversa Seam acumulativa no servidor. Medido em 10/08/2026:
    /// CONSULTA lista 120 recursos no ramo "Não" e <b>151</b> no "Sim" — 31 que só existem lá;
    /// EXAME lista 83 e 64. E o MESMO recurso muda de formulário conforme o ramo (o 1000 pede 9
    /// campos no "Não" e 3 no "Sim").</para>
    ///
    /// <para>Escolher o tipo antes do ramo devolveria a lista do ramo errado — sem erro nenhum,
    /// como sempre acontece neste sistema.</para>
    /// </summary>
    private async Task<string> PrepararAsync(
        bool ambulatorioEstadual, string tipo, CancellationToken cancellationToken)
    {
        await AbrirEditarAsync(cancellationToken);
        await TrocarAsync(CampoSisReg, ambulatorioEstadual ? "true" : "false", cancellationToken);
        return await TrocarAsync(CampoTipo, tipo, cancellationToken);
    }

    /// <summary>
    /// Dispara o <c>onchange</c> A4J de um combo. Só re-renderiza a view — nada é gravado.
    ///
    /// <para>O id do evento (<c>form0:j_idNN</c>) sai do <c>onchange</c> lido da página, nunca de
    /// captura antiga: <c>j_id</c> é posicional e muda quando a SES-RJ recompila.</para>
    /// </summary>
    private async Task<string> TrocarAsync(
        string campo, string valor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_html)) await AbrirEditarAsync(cancellationToken);

        var evento = EventoDoCombo(_html, campo)
            ?? throw new InvalidOperationException(
                $"Não achei o onchange A4J do combo '{campo}' na aba Editar do SER.");

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [campo] = valor,
            ["AJAXREQUEST"] = RegiaoViewRoot,
            [evento] = evento,
            ["ajaxSingle"] = campo,
        };

        var resposta = await sessao.SubmeterFormAsync(
            _html, SerHtmlParser.FormPesquisa, extras, _viewState, cancellationToken);

        var html = resposta.Texto;
        if (SerHtmlParser.RedirectNoCorpo(html) is { Length: > 0 } destino)
        {
            html = await sessao.AbrirTelaAsync(destino, cancellationToken);
        }

        // A resposta é PARCIAL: não promove a base dos submits seguintes (docs/ser.md §3.4).
        _viewState = SerHtmlParser.ViewStateQualquer(html) ?? _viewState;
        return html;
    }

    // ------------------------------------------------------------------ leitura

    /// <summary>Id do <c>a4j:support</c> declarado no <c>onchange</c> do combo.</summary>
    internal static string? EventoDoCombo(string html, string campo)
    {
        var sel = Regex.Match(html, "<select[^>]*name=\"" + Regex.Escape(campo) + "\"[^>]*>");
        if (!sel.Success) return null;
        var m = Regex.Match(sel.Value, @"'similarityGroupingId'\s*:\s*'([^']+)'");
        return m.Success ? m.Groups[1].Value : null;
    }

    internal static List<SerOpcaoDto> Combo(IHtmlDocument doc, string nome)
    {
        var sel = doc.QuerySelector($"select[name=\"{nome}\"]");
        if (sel is null) return [];

        return [.. sel.QuerySelectorAll("option")
            .Select(o => new SerOpcaoDto(
                o.GetAttribute("value") ?? string.Empty,
                Espremer(o.TextContent)))
            // "Selecione..." não é opção de verdade. O Seam usa `NoSelectionConverter` na maioria
            // dos combos, mas o de ambulatório estadual usa a string literal "null" — que passaria
            // no filtro e viraria uma terceira alternativa na tela.
            .Where(o => o.Valor.Length > 0
                        && !o.Valor.Contains("NoSelectionConverter", StringComparison.Ordinal)
                        && !string.Equals(o.Valor, "null", StringComparison.Ordinal))];
    }

    /// <summary>Id do <c>&lt;a title="Pesquisar"&gt;</c> do painel de paciente, lido da página.</summary>
    internal static string? BotaoPesquisarPaciente(string html)
    {
        var m = Regex.Match(html, "<a[^>]*title=\"Pesquisar\"[^>]*>");
        if (!m.Success) return null;
        var id = Regex.Match(m.Value, "id=\"([^\"]+)\"");
        return id.Success ? id.Groups[1].Value : null;
    }

    /// <summary>Mensagens que o SER exibiu (inclui o aviso de CNS definitivo × provisório).</summary>
    internal static IReadOnlyList<string> Avisos(string html)
    {
        var doc = SerHtmlParser.Documento(html);
        // GetElementById e não seletor CSS: o id tem ":" e escapá-lo em CSS só cria armadilha.
        var div = doc.GetElementById("form0:divMensagens");
        var texto = Espremer(div?.TextContent ?? string.Empty);
        return texto.Length == 0 ? [] : [texto];
    }

    /// <summary>
    /// O cadastro que o SER devolveu, campo a campo.
    ///
    /// <para><b>O que decide tudo é o <c>disabled</c>.</b> O SER trava a identidade — nome, CPF,
    /// CNS, nascimento, sexo, mãe e raça — e input travado NÃO é enviado pelo navegador: esses
    /// valores nem chegam ao Gravar, o SER os tem do lado dele. Medido em 10/08/2026: 7 travados
    /// e 11 editáveis (nome social, endereço e os três telefones).</para>
    ///
    /// <para><b>Dois dos telefones não têm id, só <c>name</c> posicional</b> (<c>form0:j_id173</c>,
    /// <c>form0:j_id178</c>). Por isso o rótulo vem do <c>&lt;label&gt;</c> irmão e o campo vem do
    /// <c>name</c> lido na hora — chumbar o j_id daria um formulário mudo na próxima recompilação
    /// da SES-RJ, sem erro nenhum.</para>
    /// </summary>
    internal static List<SerCampoPacienteDto> CamposDoPaciente(string html)
    {
        var doc = SerHtmlParser.Documento(html);
        var painel = doc.GetElementById(PainelPaciente);
        if (painel is null) return [];

        var saida = new List<SerCampoPacienteDto>();

        foreach (var el in painel.QuerySelectorAll("input, select, textarea"))
        {
            var nome = el.GetAttribute("name");
            if (string.IsNullOrEmpty(nome)) continue;

            var tipoHtml = (el.GetAttribute("type") ?? string.Empty).ToLowerInvariant();
            if (tipoHtml is "hidden" or "submit" or "button" or "image" or "reset") continue;

            // O rótulo é o <label> do mesmo <td>. O ícone (<i>) e o asterisco vêm dentro dele.
            var label = el.ParentElement?.QuerySelector("label");
            var obrigatorio = label?.QuerySelector("span.required") is not null;
            var rotulo = Espremer(label?.TextContent ?? string.Empty).Replace("*", string.Empty).Trim(' ', ':');

            var ehSelect = string.Equals(el.TagName, "select", StringComparison.OrdinalIgnoreCase);
            List<SerOpcaoDto>? opcoes = null;
            string? valor;

            if (ehSelect)
            {
                opcoes = [.. el.QuerySelectorAll("option")
                    .Select(o => new SerOpcaoDto(
                        o.GetAttribute("value") ?? string.Empty, Espremer(o.TextContent)))
                    .Where(o => o.Valor.Length > 0)];
                valor = el.QuerySelectorAll("option")
                    .FirstOrDefault(o => o.HasAttribute("selected"))?.GetAttribute("value");
            }
            else
            {
                valor = el.GetAttribute("value");
            }

            saida.Add(new SerCampoPacienteDto(
                nome,
                rotulo,
                string.IsNullOrWhiteSpace(valor) ? null : valor,
                ehSelect ? "select" : "text",
                obrigatorio,
                Editavel: !el.HasAttribute("disabled") && !el.HasAttribute("readonly"),
                opcoes));
        }

        return saida;
    }

    /// <summary>
    /// Os CID de uma tabela de sugestões do SER.
    ///
    /// <para>Cada linha de verdade tem TRÊS células: a primeira é a coluna oculta
    /// (<c>display:none</c>) com o texto que o navegador escreve no campo — <c>(A09 ) Diarréia
    /// e gastroenterite…</c> — e as outras duas são as visíveis, código e descrição. É o texto
    /// da coluna oculta que o pedido leva de volta; o código sozinho não serve.</para>
    ///
    /// <para>A tabela vem <b>sempre</b> com uma linha escondida a mais, a <c>NothingLabel</c>
    /// ("Nenhum CID encontrado"), com UMA célula só — inclusive quando há resultado. O descarte é
    /// pela forma da linha, não pelo texto da mensagem: o texto é do SER e muda com ele.</para>
    /// </summary>
    internal static List<SerCidDto> CidsDaTabela(IReadOnlyList<IReadOnlyList<string>> linhas) =>
        [.. linhas
            .Where(l => l.Count >= 3 && l[1].Trim().Length > 0)
            .Select(l => new SerCidDto(l[1].Trim(), l[2].Trim(), l[0].Trim()))];

    /// <summary>Colapsa todo espaço em branco — o HTML do SER vem cheio de quebra e tabulação.</summary>
    private static string Espremer(string texto) =>
        string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Os campos que o SER acrescenta conforme o recurso. Cada um é o par
    /// <c>container_dinamico_id_&lt;N&gt;</c> (rótulo) + <c>dinamico_id_&lt;N&gt;</c> (o campo).
    /// </summary>
    internal static List<SerCampoDinamicoDto> CamposDinamicos(string html)
    {
        var doc = SerHtmlParser.Documento(html);
        var saida = new List<SerCampoDinamicoDto>();

        foreach (var cont in doc.QuerySelectorAll("[id^='form0:container_dinamico_id_']"))
        {
            var id = cont.Id!;
            var numero = id[(id.LastIndexOf('_') + 1)..];

            // O SCRIPT SAI ANTES DO TEXTO. Campo de data é um `rich:calendar`, que embute no
            // container um <script> com a localização inteira do calendário — e `TextContent`
            // engole isso, transformando o rótulo em "Data da coleta da biópsia://<![CDATA[
            // Richfaces.Calendar.addLocale('pt', {'weekDayLabels':[...".
            foreach (var lixo in cont.QuerySelectorAll("script, style")) lixo.Remove();

            // Radio e checkbox do SER são um <table> com UM INPUT POR OPÇÃO — todos com o mesmo
            // `name` — e o texto de cada opção num <label for="<id da opção>">. O extrator antigo
            // só lia <option>, que esses campos não têm: gravou 56 radios e 4 checkboxes com
            // ZERO opções. A tela então caía no input de texto livre — o operador digitaria onde
            // o SER exige escolha entre valores fixos — e as opções ainda vinham grudadas no
            // rótulo ("Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB").
            var marcaveis = cont.QuerySelectorAll("input[type=radio], input[type=checkbox]");

            List<SerOpcaoDto>? opcoes = null;
            string tipo;
            string? nome;

            if (marcaveis.Length > 0)
            {
                var rotuloDaOpcao = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var l in cont.QuerySelectorAll("label[for]"))
                {
                    rotuloDaOpcao[l.GetAttribute("for")!] = Espremer(l.TextContent);
                }

                opcoes = [.. marcaveis
                    .Select(i => new SerOpcaoDto(
                        i.GetAttribute("value") ?? string.Empty,
                        rotuloDaOpcao.TryGetValue(i.Id ?? string.Empty, out var r) && r.Length > 0
                            ? r
                            : i.GetAttribute("value") ?? string.Empty))
                    .Where(o => o.Valor.Length > 0)
                    .DistinctBy(o => o.Valor, StringComparer.Ordinal)];

                // Só AQUI o <label for> é removido: medido em 22 containers reais, o rótulo do
                // CAMPO nunca tem `for` e o das OPÇÕES sempre tem. Em campo comum o `for` — se um
                // dia aparecer — seria o rótulo de verdade e não pode sumir.
                foreach (var l in cont.QuerySelectorAll("label[for]")) l.Remove();

                tipo = (marcaveis[0].GetAttribute("type") ?? "radio").ToLowerInvariant();
                nome = marcaveis[0].GetAttribute("name");
            }
            else
            {
                // O `rich:calendar` NÃO posta no id base: o valor viaja num input irmão terminado
                // em `InputDate` (o mesmo padrão de `form0:dtInicialSolicitacaoInputDate` das
                // telas de busca). Guardar o id base faria a data ir para um campo que o SER
                // ignora — e o pedido seria recusado por falta de um dado que a tela mostrou
                // preenchido.
                //
                // `InputCurrentDate` existe no mesmo componente e NÃO é o campo: por isso a
                // comparação é pelo fim exato do nome.
                var calendario = cont.QuerySelectorAll("input")
                    .FirstOrDefault(i => (i.GetAttribute("name") ?? string.Empty)
                        .EndsWith("InputDate", StringComparison.Ordinal));

                var el = calendario ?? cont.QuerySelector("input, select, textarea");
                if (el is null) continue;

                tipo = calendario is not null
                    ? "date"
                    : el.TagName.ToLowerInvariant() switch
                    {
                        "select" => "select",
                        "textarea" => "textarea",
                        _ => (el.GetAttribute("type") ?? "text").ToLowerInvariant(),
                    };

                if (tipo == "select")
                {
                    opcoes = [.. el.QuerySelectorAll("option")
                        .Select(o => new SerOpcaoDto(
                            o.GetAttribute("value") ?? string.Empty,
                            Espremer(o.TextContent)))
                        .Where(o => o.Valor.Length > 0)];
                }

                nome = el.GetAttribute("name");
            }

            var texto = Espremer(cont.TextContent);

            saida.Add(new SerCampoDinamicoDto(
                numero,
                // O `name` lido da página vem antes do id montado: quem manda é o SER.
                nome is { Length: > 0 } ? nome : $"form0:dinamico_id_{numero}",
                // O asterisco do SER é a marcação de obrigatório; some do rótulo e vira flag.
                texto.Replace("*", string.Empty).Trim(' ', ':'),
                tipo,
                texto.Contains('*', StringComparison.Ordinal),
                opcoes));
        }

        return saida;
    }
}

using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
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

    /// <summary>Catálogo de recursos de um tipo (CONSULTA/EXAME) — 120 e 83 na medição.</summary>
    Task<IReadOnlyList<SerOpcaoDto>> ListarRecursosAsync(
        string tipo, CancellationToken cancellationToken);

    /// <summary>Campos dinâmicos que o SER exige para aquele recurso.</summary>
    Task<IReadOnlyList<SerCampoDinamicoDto>> ObterCamposDinamicosAsync(
        string tipo, string recurso, CancellationToken cancellationToken);
}

public sealed partial class SerNovaSolicitacaoService(
    ISerWebSessao sessao,
    ILogger<SerNovaSolicitacaoService> logger) : ISerNovaSolicitacaoService
{
    public const string CaminhoTela =
        "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam";

    /// <summary>Troca de aba: <c>_JSFFormSubmit</c>, POST comum sem <c>AJAXREQUEST</c>.</summary>
    private const string AbrirAbaEditar = "form0:editar_server_submit";

    private const string CampoTipo = "form0:comboTipoRecurso";
    private const string CampoRecurso = "form0:comboRecurso";

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
        string tipo, CancellationToken cancellationToken)
    {
        var html = await TrocarTipoAsync(tipo, cancellationToken);
        var recursos = Combo(SerHtmlParser.Documento(html), CampoRecurso);

        if (recursos.Count == 0)
        {
            throw new ValidacaoException(
                "ser.recursos_vazios",
                $"O SER não devolveu recursos para o tipo {tipo}. O layout da aba mudou?");
        }

        logger.LogInformation("SER/nova: {Qtd} recursos para {Tipo}.", recursos.Count, tipo);
        return recursos;
    }

    public async Task<IReadOnlyList<SerCampoDinamicoDto>> ObterCamposDinamicosAsync(
        string tipo, string recurso, CancellationToken cancellationToken)
    {
        // A ordem importa: o combo de Recurso só é populado DEPOIS que o Tipo é escolhido, e o
        // servidor guarda isso na conversa Seam. Pedir o recurso sem passar pelo tipo devolve
        // formulário vazio — sem erro nenhum.
        await TrocarTipoAsync(tipo, cancellationToken);
        var html = await TrocarAsync(CampoRecurso, recurso, cancellationToken);
        return [.. CamposDinamicos(html)];
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

    private Task<string> TrocarTipoAsync(string tipo, CancellationToken cancellationToken) =>
        TrocarAsync(CampoTipo, tipo, cancellationToken, reabrir: true);

    /// <summary>
    /// Dispara o <c>onchange</c> A4J de um combo. Só re-renderiza a view — nada é gravado.
    ///
    /// <para>O id do evento (<c>form0:j_idNN</c>) sai do <c>onchange</c> lido da página, nunca de
    /// captura antiga: <c>j_id</c> é posicional e muda quando a SES-RJ recompila.</para>
    /// </summary>
    private async Task<string> TrocarAsync(
        string campo, string valor, CancellationToken cancellationToken, bool reabrir = false)
    {
        if (reabrir || string.IsNullOrEmpty(_html)) await AbrirEditarAsync(cancellationToken);

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

    private static List<SerOpcaoDto> Combo(IHtmlDocument doc, string nome)
    {
        var sel = doc.QuerySelector($"select[name=\"{nome}\"]");
        if (sel is null) return [];

        return [.. sel.QuerySelectorAll("option")
            .Select(o => new SerOpcaoDto(
                o.GetAttribute("value") ?? string.Empty,
                Espremer(o.TextContent)))
            // "Selecione..." e o placeholder do Seam não são opção de verdade.
            .Where(o => o.Valor.Length > 0
                        && !o.Valor.Contains("NoSelectionConverter", StringComparison.Ordinal))];
    }

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

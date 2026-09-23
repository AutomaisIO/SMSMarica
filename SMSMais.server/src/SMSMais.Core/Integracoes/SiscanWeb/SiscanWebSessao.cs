using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Core.Integracoes.SiscanWeb;

/// <summary>
/// Sessão autenticada (web scraping) do SISCAN — Sistema de Informação do Câncer, DATASUS.
/// Mesmo stack do SER: JSF 1.2 + RichFaces 3.3.3. Protocolo medido em
/// <c>Automais.SISCAN/docs/FLUXO-NOVA-REQUISICAO.md</c>.
///
/// <para><b>É PRODUÇÃO FEDERAL, com pacientes reais.</b> Todo POST passa pela trava de
/// somente-leitura; escrever exige <see cref="SubmeterEscritaAsync"/>, que é um método separado —
/// e não um <c>bool</c> — justamente para toda escrita ser uma decisão visível na chamada, achável
/// com um grep. O <c>operacao</c> é obrigatório e vai para o log: quando perguntarem "quem fez
/// isso", a resposta tem de estar aqui.</para>
///
/// <para><b>Credencial é sempre a do operador.</b> Não existe credencial de sincronismo do SISCAN
/// no banco, e isso é de propósito: o SISCAN carimba o responsável da requisição, e gravar tudo
/// com uma conta de serviço faria a trilha federal mentir sobre a autoria. Ver
/// <c>SiscanSessaoOperadorStore</c>.</para>
/// </summary>
public interface ISiscanWebSessao
{
    /// <summary>Amarra esta instância à credencial de um operador. A senha só vive em memória.</summary>
    void UsarCredencialDoOperador(string usuario, string senha);

    /// <summary>Faz o login e confirma que a conta entrou — credencial errada falha aqui.</summary>
    Task AutenticarAsync(CancellationToken cancellationToken);

    /// <summary>Abre uma tela CLICANDO no item de menu, e devolve o HTML completo.</summary>
    Task<string> AbrirPorMenuAsync(string rotuloDoMenu, CancellationToken cancellationToken);

    /// <summary>Submete um form (leitura/navegação). A trava recusa parâmetro de escrita.</summary>
    Task<string> SubmeterFormAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        CancellationToken cancellationToken, IReadOnlySet<string>? navegacaoLiberada = null);

    /// <summary>
    /// Reproduz um <c>A4J.AJAX.Submit</c> e <b>devolve a tela já com o parcial aplicado</b> —
    /// nunca o parcial cru, que não tem o form e engana quem lê.
    /// </summary>
    Task<string> SubmeterA4JAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        IReadOnlyDictionary<string, string> parametros, CancellationToken cancellationToken,
        IReadOnlySet<string>? navegacaoLiberada = null);

    /// <summary>
    /// Submete um form <b>ESCREVENDO</b> no SISCAN: a trava não roda aqui.
    ///
    /// <para><b>Só use depois de autorização explícita para AQUELA operação.</b> Hoje existe uma:
    /// criar a requisição de mamografia.</para>
    ///
    /// <para>Recebe uma LISTA de pares, e não um dicionário, porque grupo de checkbox vai com a
    /// mesma chave repetida — "tem nódulo" pode ser mama direita <b>e</b> esquerda. Um dicionário
    /// perderia a segunda em silêncio, e silêncio aqui é dado clínico errado.</para>
    /// </summary>
    Task<string> SubmeterEscritaAsync(
        string htmlPagina, string formId, IReadOnlyCollection<KeyValuePair<string, string>> extras,
        string operacao, CancellationToken cancellationToken);

    /// <summary>Descarta a sessão em memória (força novo login na próxima chamada).</summary>
    void Reiniciar();
}

/// <summary>A trava de somente-leitura recusou a operação. É <b>bug do motor</b>, não do SISCAN.</summary>
public sealed class EscritaNoSiscanBloqueadaException(string mensagem) : Exception(mensagem);

public sealed partial class SiscanWebSessao(ILogger<SiscanWebSessao> logger)
    : ISiscanWebSessao, IDisposable
{
    public const string Provedor = "siscan";
    private const string BaseUrlPadrao = "https://siscan.saude.gov.br";
    private const string CaminhoLogin = "/login.jsf";
    private const string CaminhoIndex = "/visao/index.jsf";

    public const string MenuGerenciarExame = "GERENCIAR EXAME";

    /// <summary>
    /// Verbos que denunciam escrita. Confere contra o nome do parâmetro <b>e</b> contra o rótulo
    /// visível — uma camada só não cobre JSF, onde o botão costuma ser <c>j_idNNN</c> e o verbo só
    /// existe no texto.
    /// </summary>
    [GeneratedRegex(
        "(salvar|gravar|excluir|remover|cancelar|confirmar|incluir|alterar|editar|encerrar|"
        + "finalizar|liberar|aprovar|autorizar|laudar|assinar|enviar|novo)",
        RegexOptions.IgnoreCase)]
    private static partial Regex RegexEscrita();

    /// <summary>
    /// Componentes que a trava libera porque <b>só navegam</b>. "Novo Exame" cai no verbo "novo",
    /// mas apenas renderiza a tela do assistente: nada é gravado antes do Salvar da etapa 2.
    /// </summary>
    public static readonly IReadOnlySet<string> NavegacaoDaRequisicao =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "frm:botaoNovoExame",
            "frm:botaoAvancar",
            "frm:botaoVoltar",
        };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Sessao? _sessao;
    private Credenciais? _credencial;

    public void UsarCredencialDoOperador(string usuario, string senha)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(senha))
        {
            throw new ValidacaoException(
                "siscan.credencial_operador_incompleta",
                "Informe o e-mail e a senha do SISCAN.");
        }

        _credencial = new Credenciais(usuario.Trim(), senha);
        Reiniciar();
    }

    public void Reiniciar()
    {
        _sessao?.Dispose();
        _sessao = null;
    }

    // ------------------------------------------------------------------ público

    public async Task AutenticarAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Reiniciar();
            await GarantirSessaoAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> AbrirPorMenuAsync(string rotuloDoMenu, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);
            var index = await GetAsync(sessao, CaminhoIndex, cancellationToken);
            var doc = SiscanHtml.Documento(index);

            var item = SiscanHtml.ItemDeMenu(doc, rotuloDoMenu)
                       ?? throw new ValidacaoException(
                           "siscan.menu_indisponivel",
                           $"O item de menu '{rotuloDoMenu}' não existe para esta conta do SISCAN.");

            var formId = SiscanHtml.FormDoItemDeMenu(doc, item)
                         ?? throw new ValidacaoException(
                             "siscan.menu_sem_form",
                             $"O item '{rotuloDoMenu}' veio fora de um <form> — tela inesperada.");

            // O par que o RichFaces monta: quem disparou vai em `<form>:_link_hidden_` E em
            // `<item>:hidden`. Sem os dois, o POST volta 200 com a MESMA tela, sem erro nenhum —
            // a falha silenciosa clássica do JSF.
            var extras = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [$"{formId}:_link_hidden_"] = item,
                [$"{item}:hidden"] = item,
            };

            return await PostarAsync(sessao, doc, formId, extras, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> SubmeterFormAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        CancellationToken cancellationToken, IReadOnlySet<string>? navegacaoLiberada = null)
    {
        var doc = SiscanHtml.Documento(htmlPagina);
        GarantirLeitura(extras, doc, navegacaoLiberada);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);
            return await PostarAsync(sessao, doc, formId, extras, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> SubmeterA4JAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        IReadOnlyDictionary<string, string> parametros, CancellationToken cancellationToken,
        IReadOnlySet<string>? navegacaoLiberada = null)
    {
        var doc = SiscanHtml.Documento(htmlPagina);
        GarantirLeitura(extras, doc, navegacaoLiberada);

        // Os parâmetros também passam pela trava: num A4J é o PARÂMETRO que diz quem disparou
        // (`frm:botaoNovoExame`), não o campo. Checar só `extras` deixaria a porta aberta
        // justamente onde a ação mora.
        GarantirLeitura(parametros, doc, navegacaoLiberada);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);

            var campos = SiscanHtml.CamposDoForm(doc, formId);
            foreach (var (k, v) in extras) campos[k] = v;
            foreach (var (k, v) in parametros) campos[k] = v;
            campos["AJAXREQUEST"] = "_viewRoot";

            var parcial = await PostarCruAsync(
                sessao, doc, formId, campos, ajax: true, cancellationToken);

            // Devolvemos a TELA com o parcial aplicado. Devolver o parcial cru seria entregar um
            // documento sem <form id="frm"> — quem recebe conclui que a ação falhou, e quem o
            // reposta perde tudo que está fora da região atualizada.
            var aplicado = SiscanHtml.AplicarA4J(doc, SiscanHtml.Documento(parcial));
            return aplicado.ToHtml();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> SubmeterEscritaAsync(
        string htmlPagina, string formId, IReadOnlyCollection<KeyValuePair<string, string>> extras,
        string operacao, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(operacao))
        {
            throw new ArgumentException(
                "Toda escrita no SISCAN precisa dizer QUAL operação é — o nome vai para o log.",
                nameof(operacao));
        }

        // Warning, não Debug: escrita em sistema federal é evento raro e tem de saltar do log.
        logger.LogWarning(
            "SISCAN: ESCRITA — {Operacao} (form {Form}, {Campos} parâmetro(s)).",
            operacao, formId, extras.Count);

        var doc = SiscanHtml.Documento(htmlPagina);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);
            var campos = Combinar(SiscanHtml.CamposDoForm(doc, formId), extras);
            return await PostarCruAsync(sessao, doc, formId, campos, ajax: false, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Junta o que a tela trazia com o que vamos mandar, preservando chave repetida.
    ///
    /// <para>Quem aparece em <paramref name="extras"/> sai do que veio da tela — senão o valor
    /// antigo iria junto com o novo, e o JSF ficaria com o primeiro.</para>
    /// </summary>
    private static List<KeyValuePair<string, string>> Combinar(
        Dictionary<string, string> daTela, IReadOnlyCollection<KeyValuePair<string, string>> extras)
    {
        var sobrescritas = extras.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        var campos = daTela
            .Where(c => !sobrescritas.Contains(c.Key))
            .ToList();
        campos.AddRange(extras);
        return campos;
    }

    // ------------------------------------------------------------------ trava

    internal static void GarantirLeitura(
        IReadOnlyDictionary<string, string> extras, IHtmlDocument doc,
        IReadOnlySet<string>? navegacaoLiberada)
    {
        foreach (var chave in extras.Keys)
        {
            if (navegacaoLiberada?.Contains(chave) == true) continue;

            if (RegexEscrita().IsMatch(chave))
            {
                throw new EscritaNoSiscanBloqueadaException(
                    $"POST recusado pela trava de somente-leitura: o parâmetro '{chave}' parece "
                    + "acionar uma ação de escrita no SISCAN.");
            }

            var el = doc.GetElementById(chave) ?? doc.QuerySelector($"[name='{chave}']");
            if (el is null) continue;

            var rotulo = string.Join(' ', new[]
            {
                el.GetAttribute("value"),
                el.GetAttribute("title"),
                el.TextContent,
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

            if (RegexEscrita().IsMatch(rotulo))
            {
                var curto = rotulo.Trim();
                if (curto.Length > 60) curto = curto[..60];
                throw new EscritaNoSiscanBloqueadaException(
                    $"POST recusado pela trava de somente-leitura: o componente '{chave}' tem "
                    + $"rótulo '{curto}' — é ação de escrita no SISCAN.");
            }
        }
    }

    // ------------------------------------------------------------------ sessão

    private async Task<Sessao> GarantirSessaoAsync(CancellationToken cancellationToken)
    {
        if (_credencial is null)
        {
            throw new ValidacaoException(
                "siscan.sem_credencial",
                "Esta sessão do SISCAN não tem credencial de operador. Entre no SISCAN pelo painel.");
        }

        if (_sessao is { Logado: true }) return _sessao;

        _sessao?.Dispose();
        var sessao = new Sessao(new Uri(BaseUrlPadrao));
        _sessao = sessao;

        await LoginAsync(sessao, _credencial, cancellationToken);
        sessao.Logado = true;
        return sessao;
    }

    private async Task LoginAsync(Sessao sessao, Credenciais credencial, CancellationToken cancellationToken)
    {
        var html = await GetAsync(sessao, CaminhoLogin, cancellationToken);
        var doc = SiscanHtml.Documento(html);

        // A tela tem onsubmit="cifrar()", que troca a senha pelo SHA-256 hex dela. Mandar a senha
        // crua devolve "O endereço de e-mail ou a senha estão incorretos" — a MESMA mensagem de
        // credencial errada. Custou um diagnóstico falso de "credencial desatualizada".
        var senha = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(credencial.Senha))).ToLowerInvariant();

        var submit = doc.GetElementById(SiscanHtml.FormLogin)
            ?.QuerySelector("input[type='submit']") as IHtmlInputElement;

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["email"] = credencial.Usuario,
            ["senha"] = senha,
        };
        if (!string.IsNullOrEmpty(submit?.Name))
        {
            extras[submit.Name] = submit.GetAttribute("value") ?? "Acessar";
        }

        var resposta = await PostarAsync(sessao, doc, SiscanHtml.FormLogin, extras, cancellationToken);

        if (EhTelaDeLogin(resposta))
        {
            var mensagens = SiscanHtml.Mensagens(SiscanHtml.Documento(resposta));
            logger.LogWarning("SISCAN: login recusado para {Usuario}.", credencial.Usuario);
            throw new ValidacaoException(
                "siscan.credencial_invalida",
                mensagens.FirstOrDefault()
                ?? "O SISCAN recusou o e-mail ou a senha informados.");
        }
    }

    /// <summary>
    /// A resposta é a tela de login? É assim que o SISCAN avisa que a sessão morreu ou que a
    /// credencial não serve: HTTP 200 com o formulário de login no corpo, nunca 401.
    /// </summary>
    private static bool EhTelaDeLogin(string html) =>
        html.Contains("id=\"formLogin\"", StringComparison.OrdinalIgnoreCase)
        || html.Contains("name=\"formLogin\"", StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ transporte

    private async Task<string> PostarAsync(
        Sessao sessao, IHtmlDocument doc, string formId,
        IReadOnlyDictionary<string, string> extras, CancellationToken cancellationToken)
    {
        var campos = SiscanHtml.CamposDoForm(doc, formId);
        foreach (var (k, v) in extras) campos[k] = v;
        return await PostarCruAsync(sessao, doc, formId, campos, ajax: false, cancellationToken);
    }

    private async Task<string> PostarCruAsync(
        Sessao sessao, IHtmlDocument doc, string formId,
        IEnumerable<KeyValuePair<string, string>> campos, bool ajax, CancellationToken cancellationToken)
    {
        // POSTAR NO `action` DO FORM, NUNCA NUMA CONSTANTE — mesma regra do SER e do SISREG.
        var destino = SiscanHtml.ActionDoForm(doc, formId)
                      ?? throw new ValidacaoException(
                          "siscan.form_sem_action",
                          $"O form '{formId}' veio sem `action`. Postar em caminho constante devolve "
                          + "tela diferente da que o navegador vê.");

        using var requisicao = new HttpRequestMessage(
            HttpMethod.Post, new Uri(sessao.BaseUri, destino))
        {
            Content = new FormUrlEncodedContent(campos),
        };
        if (ajax) requisicao.Headers.Add("X-Requested-With", "XMLHttpRequest");

        using var resposta = await sessao.Http.SendAsync(requisicao, cancellationToken);
        return await resposta.Content.ReadAsStringAsync(cancellationToken);
    }

    private static async Task<string> GetAsync(
        Sessao sessao, string caminho, CancellationToken cancellationToken)
    {
        using var resposta = await sessao.Http.GetAsync(new Uri(sessao.BaseUri, caminho), cancellationToken);
        return await resposta.Content.ReadAsStringAsync(cancellationToken);
    }

    private sealed record Credenciais(string Usuario, string Senha);

    private sealed class Sessao(Uri baseUri) : IDisposable
    {
        public HttpClient Http { get; } = CriarHttp();
        public Uri BaseUri { get; } = baseUri;
        public bool Logado { get; set; }

        private static HttpClient CriarHttp()
        {
            var handler = new SocketsHttpHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                AllowAutoRedirect = true,
            };
            var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                + "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR,pt;q=0.9,en;q=0.8");
            return http;
        }

        public void Dispose() => Http.Dispose();
    }

    public void Dispose()
    {
        _sessao?.Dispose();
        _gate.Dispose();
    }
}

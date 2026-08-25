using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;

namespace SMSMais.Core.Integracoes.SernitWeb;

/// <summary>
/// Sessão autenticada (web scraping) do SERNIT — SER de Niterói
/// (<c>regulacao.niteroi.rj.gov.br</c>). Subsistema irmão do SER-RJ (ADR-0042); protocolo medido
/// em <c>Automais.SERNIT/docs/APRENDIZADOS.md</c>.
///
/// <para><b>SOMENTE LEITURA.</b> Todo POST passa por <see cref="GarantirLeitura"/>. Login e
/// pesquisa são permitidos (não alteram dado); escrita sai pela porta separada
/// <see cref="SubmeterEscritaAsync"/>.</para>
///
/// <para><b>O login difere do SER-RJ.</b> <c>/ser/login</c> só entrega o form real quando o
/// <c>Referer</c> é <c>logout.jsp</c> (senão devolve a página "AGUARDE" num loop de meta-refresh).
/// A sequência que funciona — a mesma do navegador — é: página protegida → <c>logout.jsp</c> →
/// <c>login</c> → POST. Por isso a sessão manda <b>Referer automático</b> (= última URL visitada).</para>
///
/// <para><b>Sessão única</b> por credencial, como no SER-RJ: serializada por semáforo, cookies
/// próprios (JSESSIONID + UCID do balanceador).</para>
/// </summary>
public interface ISernitWebSessao
{
    Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken);
    Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken);

    Task<string> SubmeterPesquisaAsync(
        string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
        CancellationToken cancellationToken);

    Task<RespostaSernit> SubmeterFormAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        string? viewState, CancellationToken cancellationToken);

    /// <summary>Submete um form ESCREVENDO no SERNIT: a trava de leitura não roda aqui. Use só
    /// depois de autorização explícita para AQUELA operação. <paramref name="operacao"/> vai ao log.</summary>
    Task<RespostaSernit> SubmeterEscritaAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        string? viewState, string operacao, CancellationToken cancellationToken);

    Task<string> AutenticarAvulsoAsync(string usuario, string senha, CancellationToken cancellationToken);

    /// <summary>Amarra ESTA instância à credencial de um operador (escrita assinada). A senha fica
    /// só na memória desta instância.</summary>
    void UsarCredencialDoOperador(string usuario, string senha);

    void Reiniciar();

    /// <summary>Segue o redirect A4J embutido no corpo (<c>&lt;meta name="Location"&gt;</c>).</summary>
    Task<string?> SeguirRedirectNoCorpoAsync(string corpo, CancellationToken cancellationToken);
}

/// <summary>A trava de somente-leitura recusou a operação. É bug do motor, não do SERNIT.</summary>
public sealed class EscritaNoSernitBloqueadaException(string mensagem) : Exception(mensagem);

/// <summary>A linha não oferece "Histórico da Solicitação" (situação Alta).</summary>
public sealed class HistoricoSernitIndisponivelException(string mensagem) : Exception(mensagem);

public sealed partial class SernitWebSessao(
    IServiceScopeFactory scopeFactory,
    ILogger<SernitWebSessao> logger) : ISernitWebSessao, IDisposable
{
    public const string Provedor = "sernit";
    private const string BaseUrlPadrao = "https://regulacao.niteroi.rj.gov.br";
    private const string CaminhoLogin = "/ser/login";
    private const string CaminhoLogout = "/ser/logout.jsp";
    private const string CaminhoHome = "/ser/home.seam";

    public const string CaminhoPesquisa =
        "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam";

    private const string ModuloPadrao = "ambulatorial";

    /// <summary>Verbos de ESCRITA — confere contra o nome do parâmetro E o rótulo visível do
    /// componente (o SERNIT usa ids opacos; o verbo só existe no texto).</summary>
    [GeneratedRegex(
        "(salvar|gravar|confirmar|inserir|incluir|alterar|editar|atualizar|excluir|remover|deletar|"
        + "apagar|cancelar|agendar|marcar|desmarcar|reservar|autorizar|negar|devolver|encaminhar|"
        + "executar|efetivar|finalizar|aprovar|reprovar|transferir|faltou|absenteismo|registrar|"
        + "followup|follow.?up|pendenciar|dar.?alta)",
        RegexOptions.IgnoreCase)]
    private static partial Regex RegexEscrita();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Sessao? _sessao;
    private Credenciais? _credencialDoOperador;

    public void UsarCredencialDoOperador(string usuario, string senha)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(senha))
        {
            throw new ValidacaoException(
                "sernit.credencial_operador_incompleta", "Informe o usuário e a senha do SERNIT.");
        }

        _credencialDoOperador = new Credenciais(usuario, senha, new Uri(BaseUrlPadrao));
        Reiniciar();
    }

    public void Reiniciar()
    {
        _sessao?.Dispose();
        _sessao = null;
    }

    // ------------------------------------------------------------------ público

    public Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken) =>
        AbrirTelaAsync(CaminhoPesquisa, cancellationToken);

    public async Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);
            var html = await GetAsync(sessao, caminho, cancellationToken);

            if (html is null)
            {
                logger.LogInformation("SERNIT: tela {Caminho} recusada — reativando o módulo.", caminho);
                await EntrarNoModuloAsync(sessao, cancellationToken);
                html = await GetAsync(sessao, caminho, cancellationToken);
            }

            // Sessão morta no SERNIT devolve a página de login/AGUARDE (HTTP 200), nunca 401.
            if (html is not null && EhTelaDeLogin(html))
            {
                logger.LogWarning(
                    "SERNIT: a sessão tinha morrido (login/AGUARDE em {Caminho}). Reautenticando.", caminho);

                Reiniciar();
                sessao = await GarantirSessaoAsync(cancellationToken);
                html = await GetAsync(sessao, caminho, cancellationToken);

                if (html is not null && EhTelaDeLogin(html))
                {
                    throw new ValidacaoException(
                        "sernit.sessao_nao_recuperada",
                        "O SERNIT devolveu a tela de login mesmo depois de reautenticar. A credencial "
                        + "pode ter sido bloqueada, ou o SERNIT está recusando novas sessões.");
                }
            }

            return html ?? throw new ValidacaoException(
                "sernit.tela_indisponivel",
                $"O SERNIT recusou a tela {caminho}. Verifique se a credencial tem acesso ao módulo "
                + "Ambulatorial.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> SubmeterPesquisaAsync(
        string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
        CancellationToken cancellationToken)
    {
        var resposta = await SubmeterFormAsync(
            htmlForm, SernitHtmlParser.FormPesquisa, extras, viewState, cancellationToken);
        return resposta.Texto;
    }

    public Task<RespostaSernit> SubmeterFormAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        string? viewState, CancellationToken cancellationToken)
    {
        GarantirLeitura(extras, SernitHtmlParser.Documento(htmlPagina));
        return SubmeterAsync(htmlPagina, formId, extras, viewState, comoNavegador: false, cancellationToken);
    }

    public Task<RespostaSernit> SubmeterEscritaAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        string? viewState, string operacao, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(operacao))
        {
            throw new ArgumentException(
                "Toda escrita no SERNIT precisa dizer QUAL operação é — o nome vai para o log.",
                nameof(operacao));
        }

        logger.LogWarning(
            "SERNIT: ESCRITA — {Operacao} (form {Form}, {Campos} parâmetro(s)).",
            operacao, formId, extras.Count);

        return SubmeterAsync(htmlPagina, formId, extras, viewState, comoNavegador: true, cancellationToken);
    }

    private async Task<RespostaSernit> SubmeterAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        string? viewState, bool comoNavegador, CancellationToken cancellationToken)
    {
        var doc = SernitHtmlParser.Documento(htmlPagina);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);

            var campos = SernitHtmlParser.CamposDoForm(doc, formId, comoNavegador);
            campos[formId] = formId;
            foreach (var (k, v) in extras) campos[k] = v;

            var vs = viewState
                     ?? SernitHtmlParser.ViewStateDoForm(doc, formId)
                     ?? sessao.UltimoViewState;
            if (!string.IsNullOrEmpty(vs)) campos["javax.faces.ViewState"] = vs;

            if (campos.ContainsKey("AJAXREQUEST")) campos["AJAX:EVENTS_COUNT"] = "1";

            // POSTAR NO `action` DO FORM, NUNCA EM CONSTANTE (docs/ser.md §3.3, confirmado no SERNIT:
            // postar o Gravar da aba Editar no caminho de pesquisa DERRUBA a sessão).
            var destino = SernitHtmlParser.ActionDoForm(doc, formId)
                          ?? throw new ValidacaoException(
                              "sernit.form_sem_action",
                              $"O form '{formId}' da tela do SERNIT veio sem `action`.");

            var resposta = await PostAsync(sessao, destino, campos, cancellationToken);

            if (resposta.EhTexto && EhTelaDeLogin(resposta.Texto))
            {
                logger.LogWarning("SERNIT: a sessão morreu durante um POST em {Destino}.", destino);
                Reiniciar();
                throw new ValidacaoException(
                    "sernit.sessao_expirada",
                    "A sessão do SERNIT caiu no meio da operação. A próxima tentativa reautentica "
                    + "sozinha — refaça a rodada.");
            }

            AbsorverViewState(sessao, resposta.Texto);
            return resposta;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> AutenticarAvulsoAsync(
        string usuario, string senha, CancellationToken cancellationToken)
    {
        var sessao = new Sessao(new Uri(BaseUrlPadrao));
        try
        {
            await LoginAsync(sessao, usuario, senha, cancellationToken);
            await EntrarNoModuloAsync(sessao, cancellationToken);
            var html = await GetAsync(sessao, CaminhoPesquisa, cancellationToken);
            return html ?? throw new ValidacaoException(
                "sernit.sem_acesso_ambulatorio",
                "O SERNIT autenticou, mas a credencial não abriu o módulo Ambulatorial.");
        }
        finally
        {
            sessao.Dispose();
        }
    }

    public async Task<string?> SeguirRedirectNoCorpoAsync(string corpo, CancellationToken cancellationToken)
    {
        var destino = SernitHtmlParser.RedirectNoCorpo(corpo);
        if (string.IsNullOrWhiteSpace(destino)) return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);
            var html = await GetAsync(sessao, destino, cancellationToken);
            if (html is not null) AbsorverViewState(sessao, html);
            return html;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ------------------------------------------------------------------ trava

    private static readonly HashSet<string> NavegacaoLiberada = new(StringComparer.Ordinal)
    {
        "form0:editar_server_submit",
        "form0:pesquisar_server_submit",
    };

    internal static void GarantirLeitura(IReadOnlyDictionary<string, string> extras, IHtmlDocument doc)
    {
        foreach (var chave in extras.Keys)
        {
            if (NavegacaoLiberada.Contains(chave)) continue;

            if (RegexEscrita().IsMatch(chave))
            {
                throw new EscritaNoSernitBloqueadaException(
                    $"POST recusado pela trava de somente-leitura: o parâmetro '{chave}' parece "
                    + "acionar uma ação de escrita no SERNIT.");
            }

            var el = doc.GetElementById(chave) ?? doc.QuerySelector($"[name=\"{CssEscape(chave)}\"]");
            if (el is null) continue;

            var rotulo = string.Join(' ', new[]
            {
                el.GetAttribute("value"), el.GetAttribute("title"), el.TextContent,
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

            if (RegexEscrita().IsMatch(rotulo))
            {
                var curto = rotulo.Trim();
                if (curto.Length > 60) curto = curto[..60];
                throw new EscritaNoSernitBloqueadaException(
                    $"POST recusado pela trava de somente-leitura: o componente '{chave}' tem "
                    + $"rótulo '{curto}' — é ação de escrita no SERNIT.");
            }
        }
    }

    private static string CssEscape(string valor) => valor.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// A resposta é a tela de login/AGUARDE? É assim que o SERNIT avisa que a sessão morreu: HTTP
    /// 200 com o form de login OU com a splash "AGUARDE" que reflete para <c>logout.jsp</c>.
    /// </summary>
    internal static bool EhTelaDeLogin(string html) =>
        html.Contains("name=\"login:password\"", StringComparison.Ordinal)
        || html.Contains("id=\"login:password\"", StringComparison.Ordinal)
        || (html.Contains("AGUARDE", StringComparison.OrdinalIgnoreCase)
            && html.Contains("logout.jsp", StringComparison.OrdinalIgnoreCase));

    /// <summary>O form de login real foi servido? (a splash AGUARDE não tem o campo de senha).</summary>
    private static bool EhFormDeLoginReal(string html) =>
        html.Contains("name=\"login:password\"", StringComparison.Ordinal)
        || html.Contains("id=\"login:password\"", StringComparison.Ordinal);

    // ------------------------------------------------------------------ navegação

    private async Task<Sessao> GarantirSessaoAsync(CancellationToken cancellationToken)
    {
        if (_sessao is { Logado: true, ModuloAtivo: true }) return _sessao;

        var creds = await CarregarCredenciaisAsync(cancellationToken);
        _sessao?.Dispose();
        _sessao = new Sessao(creds.BaseUri);

        await LoginAsync(_sessao, creds.Usuario, creds.Senha, cancellationToken);
        await EntrarNoModuloAsync(_sessao, cancellationToken);
        return _sessao;
    }

    /// <summary>
    /// Login do SERNIT: a sequência com Referer que destrava o form (docstring do tipo).
    /// </summary>
    private async Task LoginAsync(Sessao sessao, string usuario, string senha, CancellationToken cancellationToken)
    {
        sessao.Logado = false;
        sessao.ModuloAtivo = false;

        // O form real só vem em /ser/login quando o Referer é logout.jsp. O Referer é automático
        // (= última URL). Acende a conversa numa tela protegida, passa pelo logout e pede o login.
        await GetAsync(sessao, CaminhoPesquisa, cancellationToken);
        await GetAsync(sessao, CaminhoLogout, cancellationToken);
        var pagina = await GetAsync(sessao, CaminhoLogin, cancellationToken)
            ?? throw new ValidacaoException("sernit.login_indisponivel", "A tela de login do SERNIT não respondeu.");

        // Se veio a splash AGUARDE em vez do form, refaz uma vez o par logout→login.
        if (!EhFormDeLoginReal(pagina))
        {
            await GetAsync(sessao, CaminhoLogout, cancellationToken);
            pagina = await GetAsync(sessao, CaminhoLogin, cancellationToken)
                ?? throw new ValidacaoException("sernit.login_indisponivel", "A tela de login do SERNIT não respondeu.");
        }

        if (!EhFormDeLoginReal(pagina))
        {
            throw new ValidacaoException(
                "sernit.form_login_ausente",
                "O SERNIT não entregou o formulário de login (ficou na tela AGUARDE). O fluxo de "
                + "Referer/logout mudou? Ver Automais.SERNIT/docs/APRENDIZADOS.md §3.");
        }

        var doc = SernitHtmlParser.Documento(pagina);
        var action = SernitHtmlParser.ActionDoForm(doc, "login")
            ?? throw new ValidacaoException("sernit.form_sem_action", "O form de login do SERNIT veio sem `action`.");

        var campos = SernitHtmlParser.CamposDoForm(doc, "login");
        campos["login"] = "login";
        campos["login:username"] = usuario;
        campos["login:password"] = senha;
        if (SernitHtmlParser.BotaoLogin(doc) is { } botao) campos[botao.Nome] = botao.Valor;
        campos["javax.faces.ViewState"] = SernitHtmlParser.ViewStateDoForm(doc, "login") ?? "j_id1";

        var html = (await PostAsync(sessao, action, campos, cancellationToken)).Texto;

        if (EhFormDeLoginReal(html))
        {
            throw new ValidacaoException(
                "sernit.login_falhou",
                _credencialDoOperador is not null
                    ? "Usuário ou senha do SERNIT inválidos. Confira os dados e tente de novo — é a "
                      + "mesma credencial com que você entra no site do SERNIT."
                    : "Não foi possível autenticar no SERNIT. Verifique o usuário e a senha cadastrados "
                      + "na Configuração da Regulação.");
        }

        sessao.Logado = true;
        AbsorverViewState(sessao, html);
    }

    /// <summary>Ativa o módulo Ambulatorial na sessão Seam (goModulo). <c>AJAXREQUEST</c> é
    /// obrigatório — sem ele o WildFly re-renderiza a home e a ação nem roda (HTTP 200, sem erro).</summary>
    private async Task EntrarNoModuloAsync(Sessao sessao, CancellationToken cancellationToken)
    {
        var home = await GetAsync(sessao, CaminhoHome, cancellationToken)
            ?? throw new ValidacaoException("sernit.home_indisponivel", "A home do SERNIT não respondeu.");

        var formId = SernitHtmlParser.FormDeModulo(home)
            ?? throw new ValidacaoException(
                "sernit.home_sem_modulos",
                "Não foi possível localizar a escolha de módulo na home do SERNIT (layout mudou?).");

        var doc = SernitHtmlParser.Documento(home);
        var campos = SernitHtmlParser.CamposDoForm(doc, formId);
        campos[formId] = formId;
        campos[$"{formId}:goModulo"] = $"{formId}:goModulo";
        campos["param1"] = ModuloPadrao;
        campos["AJAXREQUEST"] = formId;
        campos["AJAX:EVENTS_COUNT"] = "1";
        var vs = SernitHtmlParser.ViewStateDoForm(doc, formId) ?? SernitHtmlParser.ViewStateQualquer(home);
        if (!string.IsNullOrEmpty(vs)) campos["javax.faces.ViewState"] = vs;

        var acaoModulo = SernitHtmlParser.ActionDoForm(doc, formId)
            ?? throw new ValidacaoException("sernit.form_sem_action", $"O form '{formId}' da home do SERNIT veio sem `action`.");
        var resposta = await PostAsync(sessao, acaoModulo, campos, cancellationToken);

        var destino = resposta.Location ?? SernitHtmlParser.RedirectNoCorpo(resposta.Texto);
        if (string.IsNullOrWhiteSpace(destino))
        {
            throw new ValidacaoException(
                "sernit.modulo_nao_ativou",
                $"O SERNIT não redirecionou após escolher o módulo '{ModuloPadrao}'. AJAXREQUEST "
                + "deixou de ser aceito? Ver Automais.SERNIT/docs/APRENDIZADOS.md.");
        }

        await GetAsync(sessao, destino, cancellationToken);
        sessao.ModuloAtivo = true;
    }

    // ------------------------------------------------------------------ HTTP

    /// <summary>GET autenticado, com Referer automático. <c>null</c> em 5xx.</summary>
    private static async Task<string?> GetAsync(Sessao sessao, string caminho, CancellationToken cancellationToken)
    {
        var uri = new Uri(sessao.BaseUri, caminho);
        // O SERNIT (atrás do proxy, sem X-Forwarded-Proto) emite Location/redirect em http://. Nunca
        // falamos http com ele: sobe para https no mesmo host. Sem isto, o HttpClient recusa seguir o
        // 302 https→http (devolve corpo vazio) e o cookie de sessão Secure não iria. É o que fazia a
        // aba Editar voltar "sem o combo de Tipo". Caminho relativo passa direto (já resolve em https).
        if (uri.Scheme == Uri.UriSchemeHttp
            && string.Equals(uri.Host, sessao.BaseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            uri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
        }
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, uri);
        if (sessao.Referer is { } r) requisicao.Headers.TryAddWithoutValidation("Referer", r);

        using var resposta = await sessao.Http.SendAsync(requisicao, cancellationToken);
        sessao.Referer = uri.ToString();
        var bytes = await resposta.Content.ReadAsByteArrayAsync(cancellationToken);
        return (int)resposta.StatusCode >= 500 ? null : Encoding.UTF8.GetString(bytes);
    }

    private static async Task<RespostaSernit> PostAsync(
        Sessao sessao, string caminho, IReadOnlyDictionary<string, string> campos,
        CancellationToken cancellationToken)
    {
        using var conteudo = new FormUrlEncodedContent(campos);
        var uri = new Uri(sessao.BaseUri, caminho);
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, uri) { Content = conteudo };
        requisicao.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        if (sessao.Referer is { } r) requisicao.Headers.TryAddWithoutValidation("Referer", r);

        using var resposta = await sessao.Http.SendAsync(requisicao, cancellationToken);
        sessao.Referer = uri.ToString();
        var bytes = await resposta.Content.ReadAsByteArrayAsync(cancellationToken);

        var location = resposta.Headers.Location?.ToString()
                       ?? (resposta.Headers.TryGetValues("location", out var vs) ? vs.FirstOrDefault() : null);

        return new RespostaSernit(
            bytes,
            resposta.Content.Headers.ContentType?.MediaType,
            resposta.Content.Headers.ContentDisposition?.FileName?.Trim('"'),
            location);
    }

    private static void AbsorverViewState(Sessao sessao, string html)
    {
        var vs = SernitHtmlParser.ViewStateQualquer(html);
        if (!string.IsNullOrEmpty(vs)) sessao.UltimoViewState = vs;
    }

    // ------------------------------------------------------------------ credencial

    private sealed record Credenciais(string Usuario, string Senha, Uri BaseUri);

    private async Task<Credenciais> CarregarCredenciaisAsync(CancellationToken cancellationToken)
    {
        if (_credencialDoOperador is { } doOperador)
        {
            return doOperador with { BaseUri = new Uri(await BaseUrlConfiguradaAsync(cancellationToken)) };
        }

        using var scope = scopeFactory.CreateScope();
        var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
        var ctx = await credenciais.ObterContextoAsync(Provedor, cancellationToken);

        if (string.IsNullOrWhiteSpace(ctx.ClientId) || string.IsNullOrWhiteSpace(ctx.ClientSecret))
        {
            throw new ValidacaoException(
                "sernit.credencial_incompleta",
                "Configure o usuário e a senha do SERNIT em Regulação → Configuração → SERNIT.");
        }

        return new Credenciais(ctx.ClientId!, ctx.ClientSecret!, new Uri(LerBaseUrl(ctx.ParametrosJson)));
    }

    private async Task<string> BaseUrlConfiguradaAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
            var ctx = await credenciais.ObterContextoAsync(Provedor, cancellationToken);
            return LerBaseUrl(ctx.ParametrosJson);
        }
        catch (ValidacaoException)
        {
            return BaseUrlPadrao;
        }
    }

    private static string LerBaseUrl(string? parametrosJson)
    {
        if (string.IsNullOrWhiteSpace(parametrosJson)) return BaseUrlPadrao;
        try
        {
            using var doc = JsonDocument.Parse(parametrosJson);
            if (doc.RootElement.TryGetProperty("baseUrl", out var b)
                && b.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(b.GetString()))
            {
                return b.GetString()!;
            }
        }
        catch (JsonException)
        {
            // parametrosJson inválido → default.
        }
        return BaseUrlPadrao;
    }

    /// <summary>Uma sessão do SERNIT: cookie jar próprio (JSESSIONID + UCID) e Referer corrente.</summary>
    private sealed class Sessao(Uri baseUri) : IDisposable
    {
        public HttpClient Http { get; } = CriarHttp();
        public Uri BaseUri { get; } = baseUri;
        public bool Logado { get; set; }
        public bool ModuloAtivo { get; set; }

        /// <summary>Referer da PRÓXIMA requisição (= última URL visitada). É o que destrava o form
        /// de login do SERNIT.</summary>
        public string? Referer { get; set; }

        public string? UltimoViewState { get; set; }

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

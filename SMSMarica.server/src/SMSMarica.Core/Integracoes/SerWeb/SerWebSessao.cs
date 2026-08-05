using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Credenciais;

namespace SMSMarica.Core.Integracoes.SerWeb;

/// <summary>
/// Sessão autenticada (web scraping) do SER — Sistema Estadual de Regulação da SES-RJ.
/// Protocolo completo em <c>docs/ser.md</c>; decisão em ADR-0042.
///
/// <para><b>SOMENTE LEITURA.</b> Todo POST passa por <see cref="GarantirLeitura"/>. Login e
/// formulários de PESQUISA são permitidos porque não alteram dado; qualquer componente cujo nome
/// ou rótulo visível sugira escrita é recusado antes de sair da máquina.</para>
///
/// <para><b>Sessão única:</b> como no SISREG, um login novo derruba a sessão anterior daquele
/// operador — inclusive a do humano. Mantemos um cliente com cookies próprios e serializamos as
/// chamadas por semáforo, para que duas varreduras não se derrubem.</para>
/// </summary>
public interface ISerWebSessao
{
    /// <summary>Garante sessão logada + módulo ativo e devolve a tela de pesquisa (HTML completo).</summary>
    Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken);

    /// <summary>Submete o form da tela de pesquisa. <paramref name="htmlForm"/> é o último HTML
    /// COMPLETO (com <c>&lt;form id="form0"&gt;</c>); a resposta de paginação é parcial.</summary>
    Task<string> SubmeterPesquisaAsync(
        string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
        CancellationToken cancellationToken);

    /// <summary>Autentica uma credencial avulsa (ainda não salva) — usado na tela de configuração.</summary>
    Task<string> AutenticarAvulsoAsync(string usuario, string senha, CancellationToken cancellationToken);

    /// <summary>Descarta a sessão em memória (força novo login na próxima chamada).</summary>
    void Reiniciar();
}

/// <summary>A trava de somente-leitura recusou a operação. É <b>bug do motor</b>, não do SER.</summary>
public sealed class EscritaNoSerBloqueadaException(string mensagem) : Exception(mensagem);

/// <summary>A linha não oferece "Histórico da Solicitação" (acontece na situação Alta).</summary>
public sealed class HistoricoSerIndisponivelException(string mensagem) : Exception(mensagem);

public sealed partial class SerWebSessao(
    IServiceScopeFactory scopeFactory,
    ILogger<SerWebSessao> logger) : ISerWebSessao, IDisposable
{
    public const string Provedor = "ser";
    private const string BaseUrlPadrao = "https://ser.saude.rj.gov.br";
    private const string CaminhoLogin = "/ser/login";
    private const string CaminhoHome = "/ser/home.seam";
    private const string CaminhoModulo = "/ser/home";

    public const string CaminhoPesquisa =
        "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam";

    private const string ModuloPadrao = "ambulatorial";

    /// <summary>
    /// Verbos que denunciam ação de ESCRITA. Confere contra o nome do parâmetro <b>e</b> contra o
    /// rótulo visível do componente — uma camada só não cobre JSF: o botão <i>Gravar</i> do modal
    /// de cancelamento é <c>j_id189:j_id195</c> e <b>Registrar FollowUP</b> é <c>j_id169</c>; o
    /// verbo só existe no texto do elemento, nunca no nome.
    /// </summary>
    [GeneratedRegex(
        "(salvar|gravar|confirmar|inserir|incluir|alterar|editar|atualizar|excluir|remover|deletar|"
        + "apagar|cancelar|agendar|marcar|desmarcar|reservar|autorizar|negar|devolver|encaminhar|"
        + "executar|efetivar|finalizar|aprovar|reprovar|transferir|faltou|absenteismo|registrar|"
        + "followup|follow.?up|pendenciar|dar.?alta)",
        RegexOptions.IgnoreCase)]
    private static partial Regex RegexEscrita();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Sessao? _sessao;

    public void Reiniciar()
    {
        _sessao?.Dispose();
        _sessao = null;
    }

    // ------------------------------------------------------------------ público

    public async Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);
            var html = await GetAsync(sessao, CaminhoPesquisa, cancellationToken);

            // HTTP 500 aqui significa quase sempre "módulo não ativo na sessão" (o Seam exige a
            // navegação pelo menu). Refazemos o caminho uma vez antes de desistir.
            if (html is null)
            {
                logger.LogInformation("SER: tela de pesquisa recusada — reativando o módulo.");
                await EntrarNoModuloAsync(sessao, cancellationToken);
                html = await GetAsync(sessao, CaminhoPesquisa, cancellationToken);
            }

            return html ?? throw new ValidacaoException(
                "ser.tela_indisponivel",
                "O SER recusou a tela de pesquisa de consultas/exames. Verifique se a credencial "
                + "tem acesso ao módulo Ambulatório.");
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
        var doc = SerHtmlParser.Documento(htmlForm);
        GarantirLeitura(extras, doc);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);

            var campos = SerHtmlParser.CamposDoForm(doc, SerHtmlParser.FormPesquisa);
            campos[SerHtmlParser.FormPesquisa] = SerHtmlParser.FormPesquisa;
            foreach (var (k, v) in extras) campos[k] = v;

            // ViewState: o fresco vence. A resposta de paginação é parcial (traz a grade sem
            // form nenhum), então quem pagina passa o ViewState lido da última resposta.
            var vs = viewState
                     ?? SerHtmlParser.ViewStateDoForm(doc, SerHtmlParser.FormPesquisa)
                     ?? sessao.UltimoViewState;
            if (!string.IsNullOrEmpty(vs)) campos["javax.faces.ViewState"] = vs;
            campos["AJAX:EVENTS_COUNT"] = "1";

            var (html, _) = await PostAsync(sessao, CaminhoPesquisa, campos, cancellationToken);
            AbsorverViewState(sessao, html);
            return html;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> AutenticarAvulsoAsync(
        string usuario, string senha, CancellationToken cancellationToken)
    {
        // Cliente descartável: credencial errada não contamina o cookie jar da sessão de trabalho.
        // Ainda assim derruba a sessão humana daquele operador (sessão única no SER).
        var sessao = new Sessao(new Uri(BaseUrlPadrao));
        try
        {
            await LoginAsync(sessao, usuario, senha, cancellationToken);
            await EntrarNoModuloAsync(sessao, cancellationToken);
            var html = await GetAsync(sessao, CaminhoPesquisa, cancellationToken);
            return html ?? throw new ValidacaoException(
                "ser.sem_acesso_ambulatorio",
                "O SER autenticou, mas a credencial não abriu o módulo Ambulatório.");
        }
        finally
        {
            sessao.Dispose();
        }
    }

    // ------------------------------------------------------------------ trava

    /// <summary>
    /// Recusa POSTs que pareçam escrita. Duas camadas: nome do parâmetro (pega ids falantes) e
    /// rótulo visível do componente clicado (indispensável — o SER usa ids opacos).
    /// </summary>
    internal static void GarantirLeitura(IReadOnlyDictionary<string, string> extras, IHtmlDocument doc)
    {
        foreach (var chave in extras.Keys)
        {
            if (RegexEscrita().IsMatch(chave))
            {
                throw new EscritaNoSerBloqueadaException(
                    $"POST recusado pela trava de somente-leitura: o parâmetro '{chave}' parece "
                    + "acionar uma ação de escrita no SER.");
            }

            var el = doc.GetElementById(chave)
                     ?? doc.QuerySelector($"[name=\"{CssEscape(chave)}\"]");
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
                throw new EscritaNoSerBloqueadaException(
                    $"POST recusado pela trava de somente-leitura: o componente '{chave}' tem "
                    + $"rótulo '{curto}' — é ação de escrita no SER.");
            }
        }
    }

    private static string CssEscape(string valor) => valor.Replace("\\", "\\\\").Replace("\"", "\\\"");

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

    private async Task LoginAsync(Sessao sessao, string usuario, string senha, CancellationToken cancellationToken)
    {
        sessao.Logado = false;
        sessao.ModuloAtivo = false;

        var pagina = await GetAsync(sessao, CaminhoLogin, cancellationToken)
            ?? throw new ValidacaoException("ser.login_indisponivel", "A tela de login do SER não respondeu.");

        var doc = SerHtmlParser.Documento(pagina);
        // O action do form já vem com ";jsessionid=..." — usar cru, não reescrever.
        var action = SerHtmlParser.ActionDoForm(doc, "login") ?? CaminhoLogin;

        var campos = SerHtmlParser.CamposDoForm(doc, "login");
        campos["login"] = "login";
        campos["login:username"] = usuario;
        campos["login:password"] = senha;
        campos["login:entrar"] = "Entrar";
        campos["javax.faces.ViewState"] = SerHtmlParser.ViewStateDoForm(doc, "login") ?? "j_id1";

        var (html, _) = await PostAsync(sessao, action, campos, cancellationToken);

        // A tela de login de volta = credencial recusada.
        if (html.Contains("id=\"login:username\"", StringComparison.Ordinal)
            || html.Contains("name=\"login:password\"", StringComparison.Ordinal))
        {
            throw new ValidacaoException(
                "ser.login_falhou",
                "Não foi possível autenticar no SER. Verifique o usuário e a senha cadastrados "
                + "na Configuração da Regulação.");
        }

        sessao.Logado = true;
        AbsorverViewState(sessao, html);
    }

    /// <summary>
    /// Ativa o módulo Ambulatório na sessão Seam. <b>O parâmetro <c>AJAXREQUEST</c> é
    /// obrigatório</b>: sem ele o WildFly trata o POST como postback comum, re-renderiza a home e
    /// a ação nem roda — HTTP 200, sem erro nenhum (docs/ser.md §3.1).
    /// </summary>
    private async Task EntrarNoModuloAsync(Sessao sessao, CancellationToken cancellationToken)
    {
        var home = await GetAsync(sessao, CaminhoHome, cancellationToken)
            ?? throw new ValidacaoException("ser.home_indisponivel", "A home do SER não respondeu.");

        var formId = SerHtmlParser.FormDeModulo(home)
            ?? throw new ValidacaoException(
                "ser.home_sem_modulos",
                "Não foi possível localizar a escolha de módulo na home do SER (layout mudou?).");

        var doc = SerHtmlParser.Documento(home);
        var campos = SerHtmlParser.CamposDoForm(doc, formId);
        campos[formId] = formId;
        campos[$"{formId}:goModulo"] = $"{formId}:goModulo";
        campos["param1"] = ModuloPadrao;
        campos["AJAXREQUEST"] = formId;
        campos["AJAX:EVENTS_COUNT"] = "1";
        var vs = SerHtmlParser.ViewStateDoForm(doc, formId) ?? SerHtmlParser.ViewStateQualquer(home);
        if (!string.IsNullOrEmpty(vs)) campos["javax.faces.ViewState"] = vs;

        var (corpo, location) = await PostAsync(sessao, CaminhoModulo, campos, cancellationToken);

        // A resposta é um redirect A4J: header Location (aqui) ou <meta> no corpo (histórico).
        var destino = location ?? SerHtmlParser.RedirectNoCorpo(corpo);
        if (string.IsNullOrWhiteSpace(destino))
        {
            throw new ValidacaoException(
                "ser.modulo_nao_ativou",
                $"O SER não redirecionou após escolher o módulo '{ModuloPadrao}'. Isso costuma "
                + "significar que o parâmetro AJAXREQUEST deixou de ser aceito — ver docs/ser.md.");
        }

        // Seguir o Location é o que de fato ativa o módulo na conversa Seam.
        await GetAsync(sessao, destino, cancellationToken);
        sessao.ModuloAtivo = true;
    }

    /// <summary>Segue o redirect A4J embutido no corpo e devolve a página de destino.</summary>
    public async Task<string?> SeguirRedirectNoCorpoAsync(string corpo, CancellationToken cancellationToken)
    {
        var destino = SerHtmlParser.RedirectNoCorpo(corpo);
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

    // ------------------------------------------------------------------ HTTP

    /// <summary>GET autenticado. Devolve <c>null</c> em 5xx — o chamador decide se reativa o módulo.</summary>
    private static async Task<string?> GetAsync(Sessao sessao, string caminho, CancellationToken cancellationToken)
    {
        var uri = new Uri(sessao.BaseUri, caminho);
        using var resposta = await sessao.Http.GetAsync(uri, cancellationToken);
        var corpo = await LerAsync(resposta, cancellationToken);
        return (int)resposta.StatusCode >= 500 ? null : corpo;
    }

    private static async Task<(string corpo, string? location)> PostAsync(
        Sessao sessao, string caminho, IReadOnlyDictionary<string, string> campos,
        CancellationToken cancellationToken)
    {
        using var conteudo = new FormUrlEncodedContent(campos);
        var uri = new Uri(sessao.BaseUri, caminho);
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, uri) { Content = conteudo };
        requisicao.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

        using var resposta = await sessao.Http.SendAsync(requisicao, cancellationToken);
        var corpo = await LerAsync(resposta, cancellationToken);

        // O A4J devolve 200 + header Location (não é 3xx, então o HttpClient não segue sozinho).
        var location = resposta.Headers.Location?.ToString()
                       ?? (resposta.Headers.TryGetValues("location", out var vs) ? vs.FirstOrDefault() : null);
        return (corpo, location);
    }

    private static async Task<string> LerAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        var bytes = await resposta.Content.ReadAsByteArrayAsync(cancellationToken);
        return Encoding.UTF8.GetString(bytes);
    }

    private static void AbsorverViewState(Sessao sessao, string html)
    {
        var vs = SerHtmlParser.ViewStateQualquer(html);
        if (!string.IsNullOrEmpty(vs)) sessao.UltimoViewState = vs;
    }

    // ------------------------------------------------------------------ credencial

    private sealed record Credenciais(string Usuario, string Senha, Uri BaseUri);

    private async Task<Credenciais> CarregarCredenciaisAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
        var ctx = await credenciais.ObterContextoAsync(Provedor, cancellationToken);

        if (string.IsNullOrWhiteSpace(ctx.ClientId) || string.IsNullOrWhiteSpace(ctx.ClientSecret))
        {
            throw new ValidacaoException(
                "ser.credencial_incompleta",
                "Configure o usuário e a senha do SER em Regulação → Configuração → SER.");
        }

        return new Credenciais(ctx.ClientId!, ctx.ClientSecret!, new Uri(LerBaseUrl(ctx.ParametrosJson)));
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

    /// <summary>Uma sessão do SER: cookie jar próprio (JSESSIONID + SERVERID do balanceador).</summary>
    private sealed class Sessao(Uri baseUri) : IDisposable
    {
        public HttpClient Http { get; } = CriarHttp();
        public Uri BaseUri { get; } = baseUri;
        public bool Logado { get; set; }
        public bool ModuloAtivo { get; set; }

        /// <summary>ViewState mais recente — necessário porque a resposta de paginação é parcial
        /// e não traz form nenhum de onde extraí-lo.</summary>
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

using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Credenciais;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMarica.Data;

namespace SMSMarica.Core.Integracoes.SisregWeb;

/// <summary>
/// Sessões autenticadas (web scraping) do SISREG III.
///
/// <para><b>Sessão única por operador:</b> cada login novo derruba a sessão anterior daquele
/// operador — inclusive a do humano. Por isso mantemos um cliente HTTP com cookies próprios
/// <b>por unidade</b> (cada unidade tem seu operador) e só refazemos login quando a sessão cai.
/// As chamadas de uma mesma unidade são serializadas por semáforo; unidades diferentes correm
/// em paralelo sem se derrubar.</para>
///
/// <para><b>Credencial:</b> resolve a credencial <b>da unidade</b>
/// (<c>sisreg_credencial_unidade</c>) e, se a unidade não tiver uma cadastrada, cai na
/// credencial <b>global</b> do store de Integrações (provedor <c>sisreg</c>) — assim nada do
/// que já roda em produção quebra enquanto as unidades vão sendo cadastradas.</para>
/// </summary>
public interface ISisregWebSessao
{
    /// <summary>POST de formulário na unidade ativa da requisição (ou credencial global). Retorna o HTML.</summary>
    Task<string> PostFormAsync(string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken);

    /// <summary>POST de formulário no contexto de uma unidade específica.</summary>
    Task<string> PostFormAsync(Guid? unidadeId, string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken);

    /// <summary>GET autenticado (usado pelo <c>sisreg_ajax</c>, que é GET com querystring).</summary>
    Task<string> GetAsync(Guid? unidadeId, string caminho, IReadOnlyDictionary<string, string>? query, CancellationToken cancellationToken);

    /// <summary>Identidade da sessão (operador/perfil/unidade+CNES) para o double-check de unidade.</summary>
    Task<SisregSessaoInfo> ObterSessaoInfoAsync(Guid? unidadeId, CancellationToken cancellationToken);

    /// <summary>
    /// Autentica uma credencial <b>avulsa</b> (ainda não salva) e devolve a identidade da sessão.
    /// Usado na troca de usuário/senha: só gravamos depois que o SISREG aceitou e a unidade conferiu.
    /// </summary>
    Task<SisregSessaoInfo> AutenticarAvulsoAsync(string usuario, string senha, CancellationToken cancellationToken);
}

public sealed class SisregWebSessao(
    IServiceScopeFactory scopeFactory,
    ILogger<SisregWebSessao> logger) : ISisregWebSessao, IDisposable
{
    public const string Provedor = "sisreg";
    private const string BaseUrlPadrao = "https://sisregiii.saude.gov.br";

    /// <summary>Chave usada quando não há unidade no contexto (credencial global).</summary>
    private static readonly Guid ChaveGlobal = Guid.Empty;

    private readonly ConcurrentDictionary<Guid, SessaoUnidade> _sessoes = new();

    public Task<string> PostFormAsync(
        string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken) =>
        PostFormAsync(ResolverUnidadeDaRequisicao(), caminho, campos, cancellationToken);

    public async Task<string> PostFormAsync(
        Guid? unidadeId, string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken)
    {
        var sessao = ObterSessao(unidadeId);
        await sessao.Gate.WaitAsync(cancellationToken);
        try
        {
            var creds = await CarregarCredenciaisAsync(unidadeId, cancellationToken);
            sessao.BaseUri = creds.BaseUri;

            await GarantirLoginAsync(sessao, creds, cancellationToken);

            var (html, _, _) = await PostRawAsync(sessao, caminho, campos, cancellationToken);
            if (CadsusHtmlParser.SessaoInvalida(html))
            {
                // Sessão derrubada (uso concorrente do operador). Só reloga se a
                // reautenticação automática estiver LIGADA — senão devolvemos "Falha no
                // SISREG" para não derrubar a sessão do operador humano (freio de produção).
                sessao.Logado = false;
                if (!creds.AutoLogin) throw FalhaReautenticacaoDesativada();
                logger.LogInformation("SISREG: sessão expirada (unidade {Unidade}) — refazendo login.", unidadeId);
                await LoginAsync(sessao, creds.Usuario, creds.Senha, cancellationToken);
                (html, _, _) = await PostRawAsync(sessao, caminho, campos, cancellationToken);
            }

            GarantirSemCaptcha(html);
            return html;
        }
        finally
        {
            sessao.Gate.Release();
        }
    }

    public async Task<string> GetAsync(
        Guid? unidadeId, string caminho, IReadOnlyDictionary<string, string>? query, CancellationToken cancellationToken)
    {
        var sessao = ObterSessao(unidadeId);
        await sessao.Gate.WaitAsync(cancellationToken);
        try
        {
            var creds = await CarregarCredenciaisAsync(unidadeId, cancellationToken);
            sessao.BaseUri = creds.BaseUri;

            await GarantirLoginAsync(sessao, creds, cancellationToken);

            var html = await GetRawAsync(sessao, caminho, query, cancellationToken);
            if (CadsusHtmlParser.SessaoInvalida(html))
            {
                sessao.Logado = false;
                if (!creds.AutoLogin) throw FalhaReautenticacaoDesativada();
                await LoginAsync(sessao, creds.Usuario, creds.Senha, cancellationToken);
                html = await GetRawAsync(sessao, caminho, query, cancellationToken);
            }

            GarantirSemCaptcha(html);
            return html;
        }
        finally
        {
            sessao.Gate.Release();
        }
    }

    public async Task<SisregSessaoInfo> ObterSessaoInfoAsync(Guid? unidadeId, CancellationToken cancellationToken)
    {
        var html = await GetAsync(unidadeId, "/cgi-bin/index", null, cancellationToken);
        return SisregHomeParser.LerBarra(html)
            ?? throw new ValidacaoException(
                "sisreg.sessao_indeterminada",
                "Não foi possível identificar a unidade da sessão do SISREG. Verifique a credencial cadastrada.");
    }

    public async Task<SisregSessaoInfo> AutenticarAvulsoAsync(
        string usuario, string senha, CancellationToken cancellationToken)
    {
        // Cliente descartável: não contamina o cookie jar de nenhuma unidade se a credencial
        // estiver errada. Ainda assim derruba a sessão humana daquele operador (sessão única).
        var sessao = new SessaoUnidade(new Uri(BaseUrlPadrao));
        try
        {
            await LoginAsync(sessao, usuario, senha, cancellationToken);
            var html = await GetRawAsync(sessao, "/cgi-bin/index", null, cancellationToken);
            GarantirSemCaptcha(html);
            return SisregHomeParser.LerBarra(html)
                ?? throw new ValidacaoException(
                    "sisreg.sessao_indeterminada",
                    "O SISREG autenticou, mas não foi possível ler a unidade da sessão.");
        }
        finally
        {
            sessao.Dispose();
        }
    }

    // ------------------------------------------------------------------ interno

    private SessaoUnidade ObterSessao(Guid? unidadeId) =>
        _sessoes.GetOrAdd(unidadeId ?? ChaveGlobal, _ => new SessaoUnidade(new Uri(BaseUrlPadrao)));

    private async Task GarantirLoginAsync(SessaoUnidade sessao, Credenciais creds, CancellationToken cancellationToken)
    {
        if (sessao.Logado) return;
        if (!creds.AutoLogin) throw FalhaReautenticacaoDesativada();
        await LoginAsync(sessao, creds.Usuario, creds.Senha, cancellationToken);
    }

    private static void GarantirSemCaptcha(string html)
    {
        if (!SisregHomeParser.ExigeCaptcha(html)) return;
        throw new ValidacaoException(
            "sisreg.captcha_exigido",
            "O SISREG passou a exigir CAPTCHA para este operador (proteção anti-robô por volume de "
            + "acessos). Relogar não resolve: é preciso abrir o SISREG no navegador com esse usuário "
            + "e resolver o CAPTCHA. Depois disso, tente de novo.");
    }

    /// <summary>Unidade ativa da requisição (header X-Unidade-Id), quando houver contexto HTTP.</summary>
    private Guid? ResolverUnidadeDaRequisicao()
    {
        using var scope = scopeFactory.CreateScope();
        var usuarioAtual = scope.ServiceProvider.GetService<Identidade.IUsuarioAtualAccessor>();
        return usuarioAtual?.UnidadeAtivaId;
    }

    private sealed record Credenciais(string Usuario, string Senha, Uri BaseUri, bool AutoLogin);

    /// <summary>
    /// Credencial da unidade quando cadastrada e ativa; senão a global do store de Integrações.
    /// </summary>
    private async Task<Credenciais> CarregarCredenciaisAsync(Guid? unidadeId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        if (unidadeId is { } id && id != Guid.Empty)
        {
            var db = scope.ServiceProvider.GetRequiredService<SmsMaricaDbContext>();
            var daUnidade = await db.SisregCredenciaisUnidade
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UnidadeId == id && x.Ativo, cancellationToken);

            if (daUnidade is not null)
            {
                var protetor = scope.ServiceProvider.GetRequiredService<IProtetorSegredos>();
                var senha = protetor.Revelar(daUnidade.SenhaCifrada);
                // A credencial da unidade não tem toggle próprio de autoLogin: herda o global.
                var (baseUrlUnidade, autoLoginGlobal) = await LerParametrosGlobaisAsync(scope, cancellationToken);
                return new Credenciais(daUnidade.Usuario, senha, new Uri(baseUrlUnidade), autoLoginGlobal);
            }
        }

        var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
        var ctx = await credenciais.ObterContextoAsync(Provedor, cancellationToken); // lança se não configurado/inativo

        if (string.IsNullOrWhiteSpace(ctx.ClientId) || string.IsNullOrWhiteSpace(ctx.ClientSecret))
        {
            throw new ValidacaoException(
                "sisreg.credencial_incompleta",
                "Configure o usuário e a senha do SISREG — na Configuração SISREG (credencial da "
                + "unidade) ou na tela de Integrações (credencial global).");
        }

        var (baseUrl, autoLogin) = LerParametros(ctx.ParametrosJson);
        return new Credenciais(ctx.ClientId!, ctx.ClientSecret!, new Uri(baseUrl), autoLogin);
    }

    /// <summary>BaseUrl/autoLogin continuam vindo da configuração global (valem para todas as unidades).</summary>
    private static async Task<(string baseUrl, bool autoLogin)> LerParametrosGlobaisAsync(
        IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
            var ctx = await credenciais.ObterContextoAsync(Provedor, cancellationToken);
            return LerParametros(ctx.ParametrosJson);
        }
        catch (ValidacaoException)
        {
            // Sem credencial global configurada: a da unidade se vira com os defaults.
            return (BaseUrlPadrao, true);
        }
    }

    private static ValidacaoException FalhaReautenticacaoDesativada() => new(
        "sisreg.reautenticacao_desativada",
        "Falha no SISREG: a reautenticação automática está desativada para não conflitar com o "
        + "operador humano. Ligue-a na tela de Integrações quando o robô puder reconectar.");

    private async Task LoginAsync(SessaoUnidade sessao, string usuario, string senha, CancellationToken cancellationToken)
    {
        sessao.Logado = false;

        // Priming: recebe os cookies de sessão antes do POST de login.
        using (await sessao.Http.GetAsync(new Uri(sessao.BaseUri, "/"), cancellationToken)) { }

        var campos = new Dictionary<string, string>
        {
            ["usuario"] = usuario.ToUpperInvariant(),
            ["senha"] = string.Empty,
            ["senha_256"] = HashSenha(senha),
            ["etapa"] = "ACESSO",
            ["logout"] = string.Empty,
        };

        var (html, url, location) = await PostRawAsync(sessao, "/", campos, cancellationToken);
        GarantirSemCaptcha(html);
        if (!LoginOk(url, location, html))
        {
            throw new ValidacaoException(
                "sisreg.login_falhou",
                "Não foi possível autenticar no SISREG. Verifique o usuário e a senha.");
        }

        sessao.Logado = true;
    }

    private static async Task<(string html, Uri finalUrl, Uri? location)> PostRawAsync(
        SessaoUnidade sessao, string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(campos);
        using var resposta = await sessao.Http.PostAsync(new Uri(sessao.BaseUri, caminho), content, cancellationToken);
        var html = await LerConteudoAsync(resposta, cancellationToken);
        var finalUrl = resposta.RequestMessage?.RequestUri ?? new Uri(sessao.BaseUri, caminho);
        // Pós-login o SISREG responde 302 para http://.../cgi-bin/index (downgrade HTTPS→HTTP);
        // o HttpClient NÃO segue esse downgrade, então preservamos o Location p/ detectar sucesso.
        // A sessão já vem nos cookies (SESSION/ID), então as chamadas seguintes funcionam por HTTPS.
        return (html, finalUrl, resposta.Headers.Location);
    }

    private static async Task<string> GetRawAsync(
        SessaoUnidade sessao, string caminho, IReadOnlyDictionary<string, string>? query, CancellationToken cancellationToken)
    {
        var uri = new Uri(sessao.BaseUri, caminho);
        if (query is { Count: > 0 })
        {
            var qs = string.Join('&', query.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            uri = new Uri($"{uri.AbsoluteUri}{(uri.Query.Length > 0 ? "&" : "?")}{qs}");
        }

        using var resposta = await sessao.Http.GetAsync(uri, cancellationToken);
        return await LerConteudoAsync(resposta, cancellationToken);
    }

    /// <summary>
    /// As telas HTML são ASCII com entidades, mas o XML do <c>sisreg_ajax</c> é UTF-8 de verdade —
    /// ler como latin-1 corrompe acento em nome de procedimento. UTF-8 serve para os dois.
    /// </summary>
    private static async Task<string> LerConteudoAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        var bytes = await resposta.Content.ReadAsByteArrayAsync(cancellationToken);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Reproduz <c>hex_sha256(senha.toUpperCase())</c> do JS de login do SISREG.</summary>
    internal static string HashSenha(string senha) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(senha.ToUpperInvariant())));

    private static bool LoginOk(Uri finalUrl, Uri? location, string html) =>
        ApontaParaIndex(finalUrl)
        || ApontaParaIndex(location)
        || html.Contains("id=\"f_main\"", StringComparison.OrdinalIgnoreCase)
        || html.Contains("?logout=1", StringComparison.OrdinalIgnoreCase);

    private static bool ApontaParaIndex(Uri? u) =>
        u is not null && u.AbsolutePath.Contains("/cgi-bin/index", StringComparison.OrdinalIgnoreCase);

    /// <summary>Lê baseUrl e autoLogin do parametrosJson (defaults: base padrão + autoLogin ON).</summary>
    private static (string baseUrl, bool autoLogin) LerParametros(string? parametrosJson)
    {
        var baseUrl = BaseUrlPadrao;
        var autoLogin = true;
        if (string.IsNullOrWhiteSpace(parametrosJson)) return (baseUrl, autoLogin);
        try
        {
            using var doc = JsonDocument.Parse(parametrosJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("baseUrl", out var b) && b.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(b.GetString()))
            {
                baseUrl = b.GetString()!;
            }
            // autoLogin ausente = ligado (compatível com credenciais já salvas).
            if (root.TryGetProperty("autoLogin", out var a)
                && a.ValueKind is JsonValueKind.False or JsonValueKind.True)
            {
                autoLogin = a.GetBoolean();
            }
        }
        catch (JsonException)
        {
            // parametrosJson inválido → defaults.
        }
        return (baseUrl, autoLogin);
    }

    /// <summary>Uma sessão do SISREG: cookie jar próprio + serialização das chamadas.</summary>
    private sealed class SessaoUnidade(Uri baseUri) : IDisposable
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public HttpClient Http { get; } = CriarHttp();
        public Uri BaseUri { get; set; } = baseUri;
        public bool Logado { get; set; }

        private static HttpClient CriarHttp()
        {
            var handler = new SocketsHttpHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            };
            var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            return http;
        }

        public void Dispose()
        {
            Http.Dispose();
            Gate.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var sessao in _sessoes.Values) sessao.Dispose();
        _sessoes.Clear();
    }
}

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Credenciais;

namespace SMSMarica.Core.Integracoes.SisregWeb;

/// <summary>
/// Sessão autenticada (web scraping) do SISREG III. O SISREG é <b>sessão única por
/// operador</b>: cada login novo derruba a sessão anterior. Por isso mantemos <b>um</b>
/// cliente HTTP com cookies persistentes (singleton) e só refazemos login quando a sessão
/// cai (a resposta volta a ser a tela de login). As chamadas são serializadas por um
/// semáforo — o volume é baixo (auto-preenchimento de cadastro) e evita corrida de re-login.
///
/// <para>Credencial (usuário/senha) vem cifrada do store de Integrações (provedor
/// <c>sisreg</c>). BaseUrl opcional em <c>parametrosJson</c> (<c>{"baseUrl":"..."}</c>).</para>
/// </summary>
public interface ISisregWebSessao
{
    /// <summary>Faz POST de formulário garantindo sessão ativa (reloga uma vez se cair). Retorna o HTML.</summary>
    Task<string> PostFormAsync(string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken);
}

public sealed class SisregWebSessao(IServiceScopeFactory scopeFactory, ILogger<SisregWebSessao> logger)
    : ISisregWebSessao, IDisposable
{
    public const string Provedor = "sisreg";
    private const string BaseUrlPadrao = "https://sisregiii.saude.gov.br";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _http = CriarHttp();
    private Uri _baseUri = new(BaseUrlPadrao);
    private bool _logado;

    public async Task<string> PostFormAsync(
        string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var creds = await CarregarCredenciaisAsync(cancellationToken);
            _baseUri = creds.BaseUri;

            if (!_logado)
            {
                if (!creds.AutoLogin)
                {
                    throw FalhaReautenticacaoDesativada();
                }
                await LoginAsync(creds.Usuario, creds.Senha, cancellationToken);
            }

            var (html, _) = await PostRawAsync(caminho, campos, cancellationToken);
            if (CadsusHtmlParser.SessaoInvalida(html))
            {
                // Sessão derrubada (uso concorrente do operador). Só reloga se a
                // reautenticação automática estiver LIGADA — senão devolvemos "Falha no
                // SISREG" para não derrubar a sessão do operador humano (freio de produção).
                _logado = false;
                if (!creds.AutoLogin)
                {
                    throw FalhaReautenticacaoDesativada();
                }
                logger.LogInformation("SISREG: sessão expirada — refazendo login.");
                await LoginAsync(creds.Usuario, creds.Senha, cancellationToken);
                (html, _) = await PostRawAsync(caminho, campos, cancellationToken);
            }

            return html;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record Credenciais(string Usuario, string Senha, Uri BaseUri, bool AutoLogin);

    private async Task<Credenciais> CarregarCredenciaisAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
        var ctx = await credenciais.ObterContextoAsync(Provedor, cancellationToken); // lança se não configurado/inativo

        if (string.IsNullOrWhiteSpace(ctx.ClientId) || string.IsNullOrWhiteSpace(ctx.ClientSecret))
        {
            throw new ValidacaoException(
                "sisreg.credencial_incompleta",
                "Configure o usuário e a senha do SISREG na tela de Integrações.");
        }

        var (baseUrl, autoLogin) = LerParametros(ctx.ParametrosJson);
        return new Credenciais(ctx.ClientId!, ctx.ClientSecret!, new Uri(baseUrl), autoLogin);
    }

    private static ValidacaoException FalhaReautenticacaoDesativada() => new(
        "sisreg.reautenticacao_desativada",
        "Falha no SISREG: a reautenticação automática está desativada para não conflitar com o "
        + "operador humano. Ligue-a na tela de Integrações quando o robô puder reconectar.");

    private async Task LoginAsync(string usuario, string senha, CancellationToken cancellationToken)
    {
        _logado = false;

        // Priming: recebe os cookies de sessão antes do POST de login.
        using (await _http.GetAsync(new Uri(_baseUri, "/"), cancellationToken)) { }

        var campos = new Dictionary<string, string>
        {
            ["usuario"] = usuario.ToUpperInvariant(),
            ["senha"] = string.Empty,
            ["senha_256"] = HashSenha(senha),
            ["etapa"] = "ACESSO",
            ["logout"] = string.Empty,
        };

        var (html, url) = await PostRawAsync("/", campos, cancellationToken);
        if (!LoginOk(url, html))
        {
            throw new ValidacaoException(
                "sisreg.login_falhou",
                "Não foi possível autenticar no SISREG. Verifique o usuário e a senha na tela de Integrações.");
        }

        _logado = true;
    }

    private async Task<(string html, Uri url)> PostRawAsync(
        string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(campos);
        using var resposta = await _http.PostAsync(new Uri(_baseUri, caminho), content, cancellationToken);
        var html = await resposta.Content.ReadAsStringAsync(cancellationToken);
        return (html, resposta.RequestMessage?.RequestUri ?? new Uri(_baseUri, caminho));
    }

    /// <summary>Reproduz <c>hex_sha256(senha.toUpperCase())</c> do JS de login do SISREG.</summary>
    internal static string HashSenha(string senha) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(senha.ToUpperInvariant())));

    private static bool LoginOk(Uri url, string html) =>
        url.AbsolutePath.Contains("/cgi-bin/index", StringComparison.OrdinalIgnoreCase)
        || html.Contains("id=\"f_main\"", StringComparison.OrdinalIgnoreCase)
        || html.Contains("?logout=1", StringComparison.OrdinalIgnoreCase);

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
        _http.Dispose();
        _gate.Dispose();
    }
}

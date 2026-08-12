using System.Collections.Concurrent;
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
/// Sessões autenticadas (web scraping) do SISREG III.
///
/// <para><b>Sessão única por operador:</b> cada login novo derruba a sessão anterior daquele
/// operador — inclusive a do humano. Por isso mantemos um cliente HTTP com cookies próprios
/// <b>por operador</b> e só refazemos login quando a sessão cai. As chamadas de um mesmo
/// operador são serializadas por semáforo; operadores diferentes correm em paralelo.</para>
///
/// <para><b>Credencial única, global:</b> vem do store de Integrações (provedor <c>sisreg</c>) e
/// enxerga <b>todas</b> as unidades. Existiu aqui uma credencial por unidade
/// (<c>sisreg_credencial_unidade</c>), removida junto com a conferência de CNES que ela
/// sustentava — a unidade nunca foi propriedade da sessão, e sim filtro de cada consulta
/// (<c>AJAX_UPS</c> no mapeamento, <c>unidade</c> na exportação da agenda).</para>
/// </summary>
public interface ISisregWebSessao
{
    /// <summary>POST de formulário autenticado. Retorna o HTML.</summary>
    Task<string> PostFormAsync(string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken);

    /// <summary>
    /// GET autenticado (usado pelo <c>sisreg_ajax</c>, que é GET com querystring).
    ///
    /// <para><paramref name="pareceSessaoCaida"/> existe porque a sessão cai de duas formas e só
    /// uma aparece no HTML. O <c>sisreg_ajax</c> responde <c>&lt;ROOT/&gt;</c> vazio quando a
    /// sessão morreu — que não casa com nenhum marcador de
    /// <see cref="CadsusHtmlParser.SessaoInvalida"/> e, à primeira vista, é idêntico a "não há
    /// dados". Sem isto, o relogin nunca dispara e a sessão fica morta em memória com
    /// <c>Logado = true</c>: toda chamada seguinte devolve vazio até alguém reiniciar o serviço.
    /// Quem chama sabe diferenciar; aqui só se reage.</para>
    /// </summary>
    Task<string> GetAsync(
        string caminho,
        IReadOnlyDictionary<string, string>? query,
        CancellationToken cancellationToken,
        Func<string, bool>? pareceSessaoCaida = null);
}

public sealed class SisregWebSessao(
    IServiceScopeFactory scopeFactory,
    ILogger<SisregWebSessao> logger) : ISisregWebSessao, IDisposable
{
    public const string Provedor = "sisreg";
    private const string BaseUrlPadrao = "https://sisregiii.saude.gov.br";

    /// <summary>
    /// Campo da <see cref="ValidacaoException"/> lançada quando o SISREG passa a exigir CAPTCHA.
    /// É constante porque quem varre a agenda precisa <b>reagir</b> ao CAPTCHA (parar como parcial,
    /// guardar o cursor, pausar a unidade) em vez de tratar como erro genérico — e um
    /// <c>catch (Exception)</c> cego engoliria falha de rede junto.
    /// </summary>
    public const string CodigoCaptcha = "sisreg.captcha_exigido";

    /// <summary>
    /// A exceção é o CAPTCHA anti-robô? Use em <c>catch (Exception ex) when (...)</c>.
    /// O tipo continua <see cref="ValidacaoException"/> de propósito: o middleware já a mapeia
    /// para 400, e um tipo novo cairia em 500 nas telas interativas.
    /// </summary>
    public static bool EhCaptcha(Exception excecao) =>
        excecao is ValidacaoException validacao && validacao.Erros.ContainsKey(CodigoCaptcha);

    /// <summary>
    /// Intervalo mínimo entre relogins disparados por <i>suspeita</i> (resposta vazia). Segura o
    /// caso em que o vazio é real: sem ele, um mapeamento de 97 profissionais faria 97 logins.
    /// </summary>
    private static readonly TimeSpan EsperaEntreReloginsPorSuspeita = TimeSpan.FromMinutes(1);

    /// <summary>Sessões vivas indexadas pelo operador da credencial (ver nota da classe).</summary>
    private readonly ConcurrentDictionary<string, SessaoSisreg> _sessoes =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> PostFormAsync(
        string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken)
    {
        // A credencial vem ANTES do gate porque é ela que decide qual sessão (e qual gate) usar.
        var creds = await CarregarCredenciaisAsync(cancellationToken);
        var sessao = ObterSessao(creds);
        await sessao.Gate.WaitAsync(cancellationToken);
        try
        {
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
                logger.LogInformation("SISREG: sessão expirada ({Operador}) — refazendo login.", creds.Usuario);
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
        string caminho,
        IReadOnlyDictionary<string, string>? query,
        CancellationToken cancellationToken,
        Func<string, bool>? pareceSessaoCaida = null)
    {
        var creds = await CarregarCredenciaisAsync(cancellationToken);
        var sessao = ObterSessao(creds);
        await sessao.Gate.WaitAsync(cancellationToken);
        try
        {
            sessao.BaseUri = creds.BaseUri;

            await GarantirLoginAsync(sessao, creds, cancellationToken);

            var html = await GetRawAsync(sessao, caminho, query, cancellationToken);

            // A suspeita (resposta vazia) é palpite, não certeza: pode ser dado que não existe
            // mesmo. Por isso ela reloga no máximo uma vez por minuto — senão uma unidade
            // legitimamente vazia viraria uma tempestade de logins, que é justamente o que
            // aproxima o CAPTCHA. O marcador de HTML continua valendo sempre: aquele é certeza.
            var suspeita = pareceSessaoCaida is not null
                && pareceSessaoCaida(html)
                && DateTime.UtcNow - sessao.UltimoLoginEm > EsperaEntreReloginsPorSuspeita;

            if (CadsusHtmlParser.SessaoInvalida(html) || suspeita)
            {
                sessao.Logado = false;
                if (!creds.AutoLogin) throw FalhaReautenticacaoDesativada();
                logger.LogInformation(
                    "SISREG: sessão caiu ({Operador}) — refazendo login{Motivo}.",
                    creds.Usuario, suspeita ? " (resposta vazia do AJAX)" : string.Empty);
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

    // ------------------------------------------------------------------ interno

    private SessaoSisreg ObterSessao(Credenciais creds) =>
        _sessoes.GetOrAdd(creds.Usuario, _ => new SessaoSisreg(creds.BaseUri));

    private async Task GarantirLoginAsync(SessaoSisreg sessao, Credenciais creds, CancellationToken cancellationToken)
    {
        if (sessao.Logado) return;
        if (!creds.AutoLogin) throw FalhaReautenticacaoDesativada();
        await LoginAsync(sessao, creds.Usuario, creds.Senha, cancellationToken);
    }

    private static void GarantirSemCaptcha(string html)
    {
        if (!SisregHomeParser.ExigeCaptcha(html)) return;
        throw new ValidacaoException(
            CodigoCaptcha,
            "O SISREG passou a exigir CAPTCHA para este operador (proteção anti-robô por volume de "
            + "acessos). Relogar não resolve: é preciso abrir o SISREG no navegador com esse usuário "
            + "e resolver o CAPTCHA. Depois disso, tente de novo.");
    }

    private sealed record Credenciais(string Usuario, string Senha, Uri BaseUri, bool AutoLogin);

    /// <summary>
    /// A credencial global do store de Integrações — uma só, para todas as unidades.
    /// </summary>
    private async Task<Credenciais> CarregarCredenciaisAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
        var ctx = await credenciais.ObterContextoAsync(Provedor, cancellationToken); // lança se não configurado/inativo

        if (string.IsNullOrWhiteSpace(ctx.ClientId) || string.IsNullOrWhiteSpace(ctx.ClientSecret))
        {
            throw new ValidacaoException(
                "sisreg.credencial_incompleta",
                "Configure o usuário e a senha do SISREG na tela de Integrações (credencial "
                + "global, usada por todas as unidades).");
        }

        var (baseUrl, autoLogin) = LerParametros(ctx.ParametrosJson);
        return new Credenciais(ctx.ClientId!, ctx.ClientSecret!, new Uri(baseUrl), autoLogin);
    }

    private static ValidacaoException FalhaReautenticacaoDesativada() => new(
        "sisreg.reautenticacao_desativada",
        "Falha no SISREG: a reautenticação automática está desativada para não conflitar com o "
        + "operador humano. Ligue-a na tela de Integrações quando o robô puder reconectar.");

    private async Task LoginAsync(SessaoSisreg sessao, string usuario, string senha, CancellationToken cancellationToken)
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
        sessao.UltimoLoginEm = DateTime.UtcNow;
    }

    private static async Task<(string html, Uri finalUrl, Uri? location)> PostRawAsync(
        SessaoSisreg sessao, string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken)
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
        SessaoSisreg sessao, string caminho, IReadOnlyDictionary<string, string>? query, CancellationToken cancellationToken)
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

    /// <summary>Uma sessão do SISREG (um operador): cookie jar próprio + serialização das chamadas.</summary>
    private sealed class SessaoSisreg(Uri baseUri) : IDisposable
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public HttpClient Http { get; } = CriarHttp();
        public Uri BaseUri { get; set; } = baseUri;
        public bool Logado { get; set; }

        /// <summary>Quando o último login concluiu (UTC). Freia o relogin por suspeita.</summary>
        public DateTime UltimoLoginEm { get; set; }

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

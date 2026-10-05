using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SMSMais.Core.Integracoes.EsusPec;

/// <summary>Cidadão como o e-SUS PEC o devolve — só o que a correção de telefone usa.</summary>
public sealed record CidadaoEsusPec(
    string Id, string? Cpf, string? Cns, string? TelefoneCelular, string? TelefoneResidencial,
    string? TelefoneContato, DateTime? AtualizadoEm);

/// <summary>Erro do PEC (HTTP ou <c>errors</c> do GraphQL), com a classificação que ele devolve.</summary>
public sealed class ErroEsusPec(string mensagem, string? classificacao = null) : Exception(mensagem)
{
    public string? Classificacao { get; } = classificacao;
}

/// <summary>
/// Cliente SOMENTE LEITURA do e-SUS APS PEC do município (o e-SUS do governo) — ADR-0067. Porta do
/// laboratório <c>Automais.esus/pec/client.py</c> (medido em 02–04/10/2026, PEC Web 5.5.28).
///
/// <para>O PEC é uma SPA sobre <c>POST /api/graphql</c> (Spring, introspecção desligada): sessão por
/// cookie + <c>XSRF-TOKEN</c> ecoado em <c>X-XSRF-TOKEN</c>; o <c>Login</c> exige o cabeçalho
/// <c>Api-Consumer-Id: ESUS_WEB_CLIENT</c>. É SESSÃO ÚNICA por usuário: o login derruba quem estiver
/// usando a mesma conta — por isso quem chama só usa este cliente dentro da janela da madrugada.</para>
///
/// <para>Trava: os únicos documentos que saem daqui são os fixos abaixo (3 mutações de SESSÃO —
/// login, selecionar acesso, logout — e 2 consultas). Nada grava no PEC.</para>
///
/// <para>Educação com o servidor dos postos: uma requisição por vez, intervalo aleatório de 1–2 s, e
/// espera crescente quando a Cloudflare devolve 502/503/504 (o PEC fica lento em horário de pico).</para>
/// </summary>
public sealed class EsusPecCliente : IDisposable
{
    public const string BaseUrlPadrao = "https://esus.marica.rj.gov.br";

    private const string MutLogin = """
        mutation Login($input: LoginInput!) { login(input: $input) { success } }
        """;
    private const string MutSelecionarAcesso = """
        mutation SelecionarAcesso($input: SelecionarAcessoInput!) { selecionarAcesso(input: $input) { id } }
        """;
    private const string MutLogout = """
        mutation Logout { logout { id } }
        """;
    private const string QSessao = """
        query Sessao { sessao { id profissional { id acessos { id tipo } } } }
        """;
    private const string QBuscaCidadao = """
        query CidadaoListing($filtro: CidadaosQueryInput!) { cidadaos(input: $filtro) { content { id cpf cns } } }
        """;
    private const string QDetalheCidadao = """
        query BuscaDetailCidadao($id: ID!) {
          cidadao(id: $id) { id cpf cns telefoneCelular telefoneResidencial telefoneContato dataAtualizado }
        }
        """;

    private readonly HttpClient _http;
    private readonly CookieContainer _cookies = new();
    private readonly Random _rnd = new();
    private DateTime _ultima = DateTime.MinValue;
    private bool _logado;

    public EsusPecCliente(string? baseUrl = null, TimeSpan? timeout = null)
    {
        var handler = new HttpClientHandler { CookieContainer = _cookies, UseCookies = true, AllowAutoRedirect = true };
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri((string.IsNullOrWhiteSpace(baseUrl) ? BaseUrlPadrao : baseUrl.Trim()).TrimEnd('/') + "/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(120),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    /// <summary>Login (sem senha em log). <paramref name="forcar"/> derruba outra sessão do mesmo
    /// usuário — só se usa de madrugada (a dona da credencial não está no sistema).</summary>
    public async Task LoginAsync(string usuario, string senha, bool forcar, CancellationToken ct)
    {
        var entrada = new JsonObject
        {
            ["username"] = new string([.. usuario.Where(char.IsDigit)]) is { Length: > 0 } d ? d : usuario,
            ["password"] = senha,
        };
        try
        {
            await GraphQlAsync(MutLogin, new JsonObject { ["input"] = entrada }, "Login", login: true, ct);
        }
        catch (ErroEsusPec e) when (forcar && e.Classificacao is "UsuarioJaLogadoException" or "PecAuthenticationException")
        {
            entrada["force"] = true;
            await GraphQlAsync(MutLogin, new JsonObject { ["input"] = entrada }, "Login", login: true, ct);
        }
        _logado = true;
    }

    /// <summary>Escolhe o acesso (lotação). Vazio = o único acesso do usuário, se houver só um.</summary>
    public async Task SelecionarAcessoAsync(string? acessoId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(acessoId))
        {
            var sessao = await GraphQlAsync(QSessao, null, "Sessao", login: false, ct);
            var acessos = sessao["sessao"]?["profissional"]?["acessos"]?.AsArray();
            if (acessos is null || acessos.Count != 1)
                throw new ErroEsusPec("O usuário do e-SUS tem mais de um acesso — informe o acesso (lotação) na credencial.");
            acessoId = acessos[0]?["id"]?.GetValue<string>();
        }
        await GraphQlAsync(MutSelecionarAcesso, new JsonObject { ["input"] = new JsonObject { ["id"] = acessoId } },
            "SelecionarAcesso", login: false, ct);
    }

    /// <summary>Busca o cidadão por CPF ou CNS (o PEC acha no município todo) e lê o detalhe.
    /// Devolve null quando não acha um cidadão com ESSE documento.</summary>
    public async Task<CidadaoEsusPec?> BuscarCidadaoAsync(string documento, CancellationToken ct)
    {
        var doc = new string([.. documento.Where(char.IsDigit)]);
        if (doc.Length is not (11 or 15)) return null;
        var lista = await GraphQlAsync(QBuscaCidadao, new JsonObject
        {
            ["filtro"] = new JsonObject { ["query"] = doc, ["pageParams"] = new JsonObject { ["page"] = 0, ["size"] = 5 } },
        }, "CidadaoListing", login: false, ct);
        var achado = lista["cidadaos"]?["content"]?.AsArray()
            .FirstOrDefault(c => Digitos(c?["cpf"]) == doc || Digitos(c?["cns"]) == doc);
        if (achado?["id"]?.GetValue<string>() is not { } id) return null;

        var det = (await GraphQlAsync(QDetalheCidadao, new JsonObject { ["id"] = id }, "BuscaDetailCidadao", login: false, ct))["cidadao"];
        if (det is null) return null;
        DateTime? atualizado = det["dataAtualizado"] is JsonValue v && v.TryGetValue<long>(out var ms)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
            : null;
        return new CidadaoEsusPec(id, Texto(det["cpf"]), Texto(det["cns"]), Texto(det["telefoneCelular"]),
            Texto(det["telefoneResidencial"]), Texto(det["telefoneContato"]), atualizado);
    }

    public async Task LogoutAsync(CancellationToken ct)
    {
        if (!_logado) return;
        try { await GraphQlAsync(MutLogout, null, "Logout", login: false, ct); }
        catch (Exception) { /* sessão vai expirar sozinha; logout é cortesia */ }
        _logado = false;
    }

    // ------------------------------------------------------------------------------------------

    private async Task<JsonObject> GraphQlAsync(string doc, JsonObject? variaveis, string operacao, bool login, CancellationToken ct)
    {
        var tentativas = login ? 1 : 5; // login nunca em laço: o PEC bloqueia a conta após N falhas
        for (var t = 1; ; t++)
        {
            await RespirarAsync(ct);
            var xsrf = await XsrfAsync(ct);
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/graphql")
            {
                Content = JsonContent.Create(new JsonObject
                {
                    ["operationName"] = operacao,
                    ["query"] = doc,
                    ["variables"] = variaveis ?? new JsonObject(),
                }),
            };
            req.Headers.Add("X-XSRF-TOKEN", xsrf);
            if (login) req.Headers.Add("Api-Consumer-Id", "ESUS_WEB_CLIENT");

            HttpResponseMessage resp;
            try
            {
                resp = await _http.SendAsync(req, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && t < tentativas)
            {
                await Task.Delay(TimeSpan.FromSeconds(20 * t), ct);
                continue;
            }
            using (resp)
            {
                if ((int)resp.StatusCode is 502 or 503 or 504 && t < tentativas)
                {
                    await Task.Delay(TimeSpan.FromSeconds(20 * t), ct);
                    continue;
                }
                var corpo = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                    throw new ErroEsusPec($"{operacao}: HTTP {(int)resp.StatusCode}");
                var json = JsonNode.Parse(corpo) as JsonObject ?? throw new ErroEsusPec($"{operacao}: resposta vazia");
                if (json["errors"] is JsonArray { Count: > 0 } erros)
                    throw new ErroEsusPec($"{operacao}: {erros[0]?["message"]}",
                        erros[0]?["extensions"]?["classification"]?.GetValue<string>());
                return json["data"] as JsonObject ?? new JsonObject();
            }
        }
    }

    private async Task<string> XsrfAsync(CancellationToken ct)
    {
        for (var t = 0; t < 4; t++)
        {
            var c = _cookies.GetCookies(_http.BaseAddress!)["XSRF-TOKEN"]?.Value;
            if (!string.IsNullOrEmpty(c)) return c;
            if (t > 0) await Task.Delay(TimeSpan.FromSeconds(15 * t), ct);
            try { using var _ = await _http.GetAsync("", ct); } catch (HttpRequestException) { }
        }
        throw new ErroEsusPec("O e-SUS não devolveu o cookie XSRF-TOKEN (fora do ar?).");
    }

    private async Task RespirarAsync(CancellationToken ct)
    {
        var alvo = _ultima.AddMilliseconds(1000 + _rnd.Next(0, 1000));
        var falta = alvo - DateTime.UtcNow;
        if (falta > TimeSpan.Zero) await Task.Delay(falta, ct);
        _ultima = DateTime.UtcNow;
    }

    private static string? Texto(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;
    private static string Digitos(JsonNode? n) => Regex.Replace(Texto(n) ?? "", @"\D", "");

    public void Dispose() => _http.Dispose();
}

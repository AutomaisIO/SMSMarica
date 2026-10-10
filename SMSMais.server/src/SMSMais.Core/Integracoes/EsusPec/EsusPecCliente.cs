using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SMSMais.Core.Integracoes.EsusPec;

/// <summary>Cidadão como o e-SUS PEC o devolve — só o que a correção de telefone usa.</summary>
public sealed record CidadaoEsusPec(
    string Id, string? Cpf, string? Cns, string? TelefoneCelular, string? TelefoneResidencial,
    string? TelefoneContato, DateTime? AtualizadoEm);

/// <summary>Cadastro do cidadão no PEC como o "Enriquecer" da ficha usa — tudo o que dá para comparar
/// com o nosso. Nascimento e óbito em <c>yyyy-MM-dd</c>; UF por extenso (o PEC não manda a sigla).</summary>
public sealed record FichaCidadaoEsusPec(
    string Id, string? Cpf, string? Cns, string? Nome, string? NomeSocial, string? DataNascimento,
    string? Sexo, string? NomeMae, string? NomePai, string? RacaCor, string? Email,
    string? TelefoneCelular, string? TelefoneResidencial, string? TelefoneContato,
    string? Cep, string? Uf, string? Municipio, string? Bairro, string? TipoLogradouro, string? Logradouro,
    string? Numero, string? Complemento, bool Faleceu, bool Ativo, DateTime? AtualizadoEm);

/// <summary>Um acesso (lotação, estágio, gestor…) do usuário logado no PEC.</summary>
public sealed record AcessoEsusPec(string Id, string Tipo, string? Unidade);

/// <summary>Como terminou o login que NÃO força a entrada.</summary>
public enum ResultadoLoginEsusPec
{
    Entrou,
    /// <summary>O usuário já tem sessão aberta em outro lugar — entrar derrubaria aquela.</summary>
    OutraSessaoAberta,
}

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
/// login, selecionar acesso, logout — e 4 consultas: sessão, acessos, busca e detalhe do cidadão).
/// Nada grava no PEC.</para>
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
    // Campos tirados do BuscaDetailCidadao do próprio front do PEC (catálogo do laboratório,
    // medido em 02/10/2026). `ativo` só vale em Lotacao/Estagio — pedir em outro tipo de acesso dá
    // FieldsConflict, por isso a lista de acessos não pede.
    private const string QAcessos = """
        query Sessao {
          sessao { profissional { acessos { id tipo
            ... on Lotacao { unidadeSaude { nome } }
            ... on Estagio { unidadeSaude { nome } } } } }
        }
        """;
    private const string QFichaCidadao = """
        query BuscaDetailCidadao($id: ID!) {
          cidadao(id: $id) {
            id cpf cns nome nomeSocial dataNascimento dataAtualizado sexo nomeMae nomePai email
            telefoneResidencial telefoneCelular telefoneContato faleceu ativo
            endereco { cep uf { nome } municipio { nome } bairro tipoLogradouro { nome } logradouro numero complemento }
            racaCor { racaCorDbEnum }
          }
        }
        """;

    private readonly HttpClient _http;
    private readonly CookieContainer _cookies = new();
    private readonly Random _rnd = new();
    private DateTime _ultima = DateTime.MinValue;
    private bool _logado;
    private readonly int _tentativas;

    /// <param name="tentativas">Tentativas por consulta quando o PEC devolve 502/503/504 (com espera
    /// crescente). A rotina da madrugada pode esperar; quem está com o painel aberto, não — o
    /// "Enriquecer" usa 2.</param>
    public EsusPecCliente(string? baseUrl = null, TimeSpan? timeout = null, int tentativas = 5)
    {
        _tentativas = Math.Clamp(tentativas, 1, 5);
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
            // Cópia: `entrada` já pertence ao pedido anterior, e um nó JSON não pode ter dois pais.
            var forcada = (JsonObject)entrada.DeepClone();
            forcada["force"] = true;
            await GraphQlAsync(MutLogin, new JsonObject { ["input"] = forcada }, "Login", login: true, ct);
        }
        _logado = true;
    }

    /// <summary>
    /// Login de quem está <b>no painel, de dia</b> (o "Enriquecer" da ficha). Sem
    /// <paramref name="forcar"/>, nunca derruba ninguém: se a conta já tem sessão aberta, devolve
    /// <see cref="ResultadoLoginEsusPec.OutraSessaoAberta"/> e quem chama pergunta à pessoa. Senha
    /// errada, conta bloqueada etc. saem como <see cref="ErroEsusPec"/> com a mensagem do PEC.
    /// Nunca repete o login em laço — o PEC bloqueia a conta após N falhas.
    /// </summary>
    public async Task<ResultadoLoginEsusPec> TentarLoginAsync(string usuario, string senha, bool forcar, CancellationToken ct)
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
        catch (ErroEsusPec e) when (OutraSessao(e))
        {
            if (!forcar) return ResultadoLoginEsusPec.OutraSessaoAberta;
            var forcada = (JsonObject)entrada.DeepClone();
            forcada["force"] = true;
            await GraphQlAsync(MutLogin, new JsonObject { ["input"] = forcada }, "Login", login: true, ct);
        }
        _logado = true;
        return ResultadoLoginEsusPec.Entrou;
    }

    /// <summary>"Você já está logado em outra sessão…" — o PEC manda como <c>UsuarioJaLogadoException</c>
    /// ou como <c>PecAuthenticationException</c> (medido no laboratório, §2.1); a senha errada também
    /// vem como <c>PecAuthenticationException</c>, por isso o texto decide.</summary>
    internal static bool OutraSessao(ErroEsusPec e) =>
        e.Classificacao == "UsuarioJaLogadoException"
        || (e.Message.Contains("logado", StringComparison.OrdinalIgnoreCase)
            && e.Message.Contains("sess", StringComparison.OrdinalIgnoreCase));

    /// <summary>Acessos do usuário logado (antes de escolher um).</summary>
    public async Task<IReadOnlyList<AcessoEsusPec>> ListarAcessosAsync(CancellationToken ct)
    {
        var sessao = await GraphQlAsync(QAcessos, null, "Sessao", login: false, ct);
        return [.. (sessao["sessao"]?["profissional"]?["acessos"]?.AsArray() ?? [])
            .Where(a => Texto(a?["id"]) is not null)
            .Select(a => new AcessoEsusPec(Texto(a!["id"])!, Texto(a["tipo"]) ?? "", Texto(a["unidadeSaude"]?["nome"])))];
    }

    /// <summary>Ficha completa do cidadão pelo CPF ou CNS; null quando não acha um com ESSE documento.</summary>
    public async Task<FichaCidadaoEsusPec?> BuscarFichaAsync(string documento, CancellationToken ct)
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

        var det = (await GraphQlAsync(QFichaCidadao, new JsonObject { ["id"] = id }, "BuscaDetailCidadao", login: false, ct))["cidadao"];
        return det is null ? null : FichaDe(det);
    }

    /// <summary>O <c>cidadao</c> do PEC → ficha. Separado para o teste não depender de HTTP.</summary>
    internal static FichaCidadaoEsusPec FichaDe(JsonNode det)
    {
        var end = det["endereco"];
        DateTime? atualizado = det["dataAtualizado"] is JsonValue v && v.TryGetValue<long>(out var ms)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
            : null;
        return new FichaCidadaoEsusPec(
            Texto(det["id"]) ?? "", Texto(det["cpf"]), Texto(det["cns"]), Texto(det["nome"]), Texto(det["nomeSocial"]),
            Texto(det["dataNascimento"]), Texto(det["sexo"]), Texto(det["nomeMae"]), Texto(det["nomePai"]),
            Texto(det["racaCor"]?["racaCorDbEnum"]), Texto(det["email"]),
            Texto(det["telefoneCelular"]), Texto(det["telefoneResidencial"]), Texto(det["telefoneContato"]),
            Texto(end?["cep"]), Texto(end?["uf"]?["nome"]), Texto(end?["municipio"]?["nome"]), Texto(end?["bairro"]),
            Texto(end?["tipoLogradouro"]?["nome"]), Texto(end?["logradouro"]), Texto(end?["numero"]),
            Texto(end?["complemento"]),
            Faleceu: det["faleceu"] is JsonValue f && f.TryGetValue<bool>(out var faleceu) && faleceu,
            Ativo: det["ativo"] is not JsonValue a || !a.TryGetValue<bool>(out var ativo) || ativo,
            atualizado);
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
        var tentativas = login ? 1 : _tentativas; // login nunca em laço: o PEC bloqueia a conta após N falhas
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
        for (var t = 0; t < Math.Clamp(_tentativas, 2, 4); t++)
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

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PuppeteerSharp;

namespace SMSMais.Integrador.Agente;

/// <summary>
/// Observa o Chrome do agente pelo CDP: toda aba/popup que nasce é anexada; nas páginas dos sítios
/// configurados captura o ENVIO (requisição) e o RETORNO (corpo de AJAX pela rede; HTML da tela
/// pelo script injetado) e injeta a UI (selo, blur, janela). Fora da lista de sítios, nada é lido.
/// Porte do que a extensão faz com <c>webRequest</c> + <c>content.js</c> + <c>capture-hook.js</c>.
/// </summary>
public sealed partial class Observador
{
    private readonly Config _cfg;
    private readonly Enviador _env;
    private readonly Action<string> _log;
    private readonly string _script;

    private readonly ConcurrentDictionary<IPage, byte> _paginas = new();
    private readonly ConcurrentDictionary<IPage, string> _operadorPorPagina = new();
    private readonly ConcurrentDictionary<string, JsonObject> _pendentes = new(); // requestId -> item
    private readonly ConcurrentDictionary<string, DateTime> _eventosRecentes = new(); // dedupe do modo mínimo

    public Observador(Config cfg, Enviador env, Action<string> log)
    {
        _cfg = cfg;
        _env = env;
        _log = log;
        _script = MontarScript();
        _env.EstadoMudou += DifundirEstadoAsync;
    }

    public async Task IniciarAsync(IBrowser browser)
    {
        browser.TargetCreated += async (_, e) =>
        {
            try
            {
                if (e.Target.Type != TargetType.Page) return;
                var page = await e.Target.PageAsync();
                if (page is not null) await AnexarAsync(page);
            }
            catch (Exception ex)
            {
                _log($"Falha ao anexar aba nova: {ex.Message}");
            }
        };

        foreach (var page in await browser.PagesAsync())
            await AnexarAsync(page);
    }

    // ------------------------------------------------------------------ anexar

    private async Task AnexarAsync(IPage page)
    {
        if (!_paginas.TryAdd(page, 0)) return;

        page.Close += (_, _) =>
        {
            _paginas.TryRemove(page, out _);
            _operadorPorPagina.TryRemove(page, out _);
        };
        page.Request += (_, e) => TratarRequisicao(page, e.Request);
        page.Response += (_, e) => _ = TratarRespostaAsync(page, e.Response);
        page.RequestFailed += (_, e) => Concluir(page, e.Request, e.Request.FailureText ?? "erro");

        // Página → agente (Runtime.addBinding). O script injetado chama window.__smsmaisIntegradorBind(json).
        await page.ExposeFunctionAsync<string, bool>("__smsmaisIntegradorBind", json =>
        {
            _ = TratarMensagemAsync(page, json);
            return true;
        });

        // Em toda navegação futura, antes do script da página.
        await page.EvaluateExpressionOnNewDocumentAsync(_script);

        // A aba pode já estar carregada (a primeira): injeta agora nos frames existentes.
        foreach (var frame in page.Frames)
        {
            try { await frame.EvaluateExpressionAsync(_script); }
            catch { /* frame navegando ou inacessível */ }
        }

        _log($"Aba anexada: {Resumir(page.Url)}");
    }

    // -------------------------------------------------------------- requisição

    private void TratarRequisicao(IPage page, IRequest req)
    {
        var sitio = _cfg.SitioDeUrl(req.Url);
        if (sitio is null) return;
        if (req.ResourceType is not (ResourceType.Document or ResourceType.Xhr or ResourceType.Fetch or ResourceType.Other)) return;

        var campos = LerCampos(req);
        var etapa = Primeiro(campos, "etapa");

        if (_cfg.Verboso && sitio.Modo == "minimo")
            _log($"  (minimo) {sitio.Id} {req.Method.Method} {Caminho(req.Url)} etapa={etapa ?? "-"} — nada enviado");

        if (sitio.Modo == "minimo")
        {
            // Só comandos de escrita cujo NÚMERO já está no envio (cancelar). O "agendou" tem o
            // número só na resposta — tratado no handler de 'resposta'. Nada de raw sai daqui.
            if (etapa is not null && Catalogo.Etapas.TryGetValue(etapa, out var cfgEtapa)
                && cfgEtapa.Comando is not null && cfgEtapa.NumeroDe == "envio" && cfgEtapa.CampoNumero is not null)
            {
                EmitirEvento(page, sitio, cfgEtapa.Comando, SoDigitos(Primeiro(campos, cfgEtapa.CampoNumero)));
            }
            return;
        }

        // modo 'analise': captura burra (envio cru).
        var item = Classificar(page, req, sitio, campos, etapa);
        if (_cfg.Verboso) _log($"  requisicao {sitio.Id} {req.Method.Method} {Caminho(req.Url)} etapa={etapa ?? "-"}");
        _pendentes[req.Id] = item;
        _env.Empilhar(item);
        _env.TalvezEnviar();
        _ = EnviarParaPaginaAsync(page, "requisicao", item);
    }

    private JsonObject Classificar(IPage page, IRequest req, Sitio sitio, JsonObject campos, string? etapa)
    {
        var caminho = Caminho(req.Url);
        Catalogo.Endpoints.TryGetValue(caminho, out var nome);
        Etapa? gatilho = etapa is not null && Catalogo.Etapas.TryGetValue(etapa, out var g) ? g : null;
        return new JsonObject
        {
            ["kind"] = "requisicao",
            ["requestId"] = req.Id,
            ["quando"] = DateTime.UtcNow.ToString("o"),
            ["metodo"] = req.Method.Method,
            ["caminho"] = caminho,
            ["conhecido"] = nome is not null,
            ["nome"] = nome ?? "desconhecido",
            ["etapa"] = etapa,
            ["evento"] = gatilho?.Evento,
            ["escrita"] = gatilho?.Escrita ?? false,
            ["operador"] = OperadorDe(page),
            ["sitio"] = sitio.Id,
            ["canal"] = "cdp",
            ["campos"] = campos,
            ["status"] = null,
        };
    }

    /// <summary>Campos da query + do corpo urlencoded, como o <c>lerCampos</c> da extensão. Senha mascarada.</summary>
    private static JsonObject LerCampos(IRequest req)
    {
        var campos = new Dictionary<string, List<string>>();

        try
        {
            var query = new Uri(req.Url).Query;
            foreach (var (k, v) in ParseUrlEncoded(query.TrimStart('?')))
                (campos.TryGetValue(k, out var l) ? l : campos[k] = []).Add(v);
        }
        catch
        {
            // url sem query
        }

        var corpo = req.PostData?.ToString();
        if (!string.IsNullOrEmpty(corpo))
        {
            if (corpo.Contains('=') && !corpo.TrimStart().StartsWith('{') && !corpo.Contains("Content-Disposition"))
            {
                foreach (var (k, v) in ParseUrlEncoded(corpo))
                    (campos.TryGetValue(k, out var l) ? l : campos[k] = []).Add(v);
            }
            else
            {
                campos["(corpo)"] = [corpo.Length > 4000 ? corpo[..4000] : corpo];
            }
        }

        var saida = new JsonObject();
        foreach (var (k, lista) in campos)
        {
            var arr = new JsonArray();
            if (Catalogo.CamposSensiveis.Contains(k)) arr.Add("••••");
            else foreach (var v in lista) arr.Add(v);
            saida[k] = arr;
        }
        return saida;
    }

    private static IEnumerable<(string, string)> ParseUrlEncoded(string texto)
    {
        foreach (var par in texto.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = par.IndexOf('=');
            var k = i < 0 ? par : par[..i];
            var v = i < 0 ? string.Empty : par[(i + 1)..];
            yield return (Decodificar(k), Decodificar(v));
        }
    }

    private static string Decodificar(string s)
    {
        try { return Uri.UnescapeDataString(s.Replace('+', ' ')); }
        catch { return s; }
    }

    // ---------------------------------------------------------------- resposta

    private async Task TratarRespostaAsync(IPage page, IResponse resp)
    {
        var req = resp.Request;
        var sitio = _cfg.SitioDeUrl(req.Url);
        if (sitio is null) return;

        Concluir(page, req, ((int)resp.Status).ToString());

        // Corpo de AJAX (o que a extensão pegava com o hook de fetch/XHR). Aqui vem da rede, para
        // TUDO — inclusive o que o hook não enxergava. Só no modo análise; no mínimo nada de raw.
        if (sitio.Modo != "analise") return;
        if (req.ResourceType is not (ResourceType.Xhr or ResourceType.Fetch)) return;

        string corpo;
        try { corpo = await resp.TextAsync(); }
        catch { return; } // corpo indisponível (redirect, cache, aba fechou)
        if (_cfg.Verboso) _log($"  ajax {sitio.Id} {req.Method.Method} {Caminho(req.Url)} {(int)resp.Status} corpo={corpo.Length} chars");

        var item = new JsonObject
        {
            ["kind"] = "ajax",
            ["via"] = "cdp",
            ["quando"] = DateTime.UtcNow.ToString("o"),
            ["operador"] = OperadorDe(page),
            ["sitio"] = sitio.Id,
            ["url"] = req.Url,
            ["caminho"] = Caminho(req.Url),
            ["metodo"] = req.Method.Method,
            ["status"] = ((int)resp.Status).ToString(),
            ["corpo"] = corpo.Length > _cfg.RespostaMaxChars ? corpo[.._cfg.RespostaMaxChars] : corpo,
        };
        _env.Empilhar(item);
        _env.TalvezEnviar();
    }

    private void Concluir(IPage page, IRequest req, string status)
    {
        if (!_pendentes.TryRemove(req.Id, out var item)) return;
        item["status"] = status;
        _ = EnviarParaPaginaAsync(page, "requisicao", item);
    }

    // ------------------------------------------------- mensagens vindas da página

    private async Task TratarMensagemAsync(IPage page, string json)
    {
        JsonObject? msg;
        try { msg = JsonNode.Parse(json) as JsonObject; }
        catch { return; }
        if (msg is null) return;

        switch (msg["tipo"]?.GetValue<string>())
        {
            case "auth":
                // Vem do script rodando no painel do SMSMais (localStorage['smsmarica.auth']).
                await _env.DefinirSessaoAsync(Enviador.LerSessao(msg["sessao"]));
                break;

            case "estado":
                await EnviarEstadoAsync(page);
                break;

            case "operador":
                // A página leu a barra "Operador:" — passa a carimbar as capturas seguintes desta aba.
                if (msg["operador"]?.GetValue<string>() is { Length: > 0 } nome)
                    _operadorPorPagina[page] = nome;
                break;

            case "resposta":
                TratarResposta(page, msg["dados"] as JsonObject);
                break;
        }
    }

    /// <summary>HTML da tela que carregou (o RETORNO), mandado pelo script de cada frame.</summary>
    private void TratarResposta(IPage page, JsonObject? dados)
    {
        if (dados is null) return;
        var sitio = _cfg.SitioDeUrl(dados["url"]?.GetValue<string>());
        if (sitio is null) return;

        var html = dados["html"]?.GetValue<string>() ?? string.Empty;
        if (_cfg.Verboso) _log($"  resposta {sitio.Id} {dados["caminho"]?.GetValue<string>()} html={html.Length} chars ({sitio.Modo})");

        if (sitio.Modo == "minimo")
        {
            // Agendar: o nº da solicitação só existe na tela de confirmação da marcação.
            // Extrai só o número (nada de HTML/PII sai daqui) e emite o evento mínimo.
            if (dados["caminho"]?.GetValue<string>() == "/cgi-bin/marcar" && ChaveDeConfirmacao().IsMatch(html))
                EmitirEvento(page, sitio, "agendou", NumeroDaConfirmacaoMarcar(html));
            return; // minimo: nada de raw
        }

        // modo 'analise': captura burra (retorno cru — HTML da tela).
        var item = new JsonObject
        {
            ["kind"] = "resposta",
            ["quando"] = DateTime.UtcNow.ToString("o"),
            ["operador"] = OperadorDe(page),
            ["sitio"] = sitio.Id,
        };
        foreach (var (k, v) in dados)
            item[k] = k == "html" ? (html.Length > _cfg.RespostaMaxChars ? html[.._cfg.RespostaMaxChars] : html) : v?.DeepClone();
        _env.Empilhar(item);
        if (_env.Pendentes >= _cfg.LoteMaxItens) _env.TalvezEnviar(forcar: true);
        else _env.TalvezEnviar();
    }

    // ------------------------------------------------------------ modo mínimo

    /// <summary>Envia SÓ o comando + o número (sem PII) e mostra na janela de tráfego. Dedupe 15 s.</summary>
    private void EmitirEvento(IPage page, Sitio sitio, string comando, string? numero)
    {
        var chave = $"{sitio.Id}:{comando}:{numero ?? "?"}";
        var agora = DateTime.UtcNow;
        if (_eventosRecentes.TryGetValue(chave, out var antes) && (agora - antes).TotalSeconds < 15) return;
        _eventosRecentes[chave] = agora;

        var item = new JsonObject
        {
            ["kind"] = "evento",
            ["sitio"] = sitio.Id,
            ["comando"] = comando,
            ["numero"] = string.IsNullOrEmpty(numero) ? null : numero,
            ["quando"] = agora.ToString("o"),
            ["operador"] = OperadorDe(page),
            ["canal"] = "cdp",
        };
        _log($"EVENTO {sitio.Label}: {comando} · solicitação {numero ?? "?"}");
        _env.Empilhar(item);
        _env.TalvezEnviar();
        _ = EnviarParaPaginaAsync(page, "trafego", item);
    }

    /// <summary>Nº da solicitação na tela de CONFIRMAÇÃO da marcação. Mesmos padrões da extensão.</summary>
    private static string? NumeroDaConfirmacaoMarcar(string html)
    {
        if (string.IsNullOrEmpty(html)) return null;
        var texto = EntidadesHtml().Replace(html, " ");
        var m = NumeroSolicitacao1().Match(texto);
        if (!m.Success) m = NumeroSolicitacao2().Match(texto);
        return m.Success ? m.Groups[1].Value : null;
    }

    [GeneratedRegex("Chave de Confirma", RegexOptions.IgnoreCase)]
    private static partial Regex ChaveDeConfirmacao();

    [GeneratedRegex(@"&#\d+;|&[a-z]+;", RegexOptions.IgnoreCase)]
    private static partial Regex EntidadesHtml();

    [GeneratedRegex(@"Solicita\w*\s*[:\-nº.]*\s*(\d{4,})", RegexOptions.IgnoreCase)]
    private static partial Regex NumeroSolicitacao1();

    [GeneratedRegex(@"N[ºo.]?\s*(?:da\s*)?Solicita\w*\s*[:\-]*\s*(\d{4,})", RegexOptions.IgnoreCase)]
    private static partial Regex NumeroSolicitacao2();

    // ------------------------------------------------------- agente → página

    private async Task DifundirEstadoAsync()
    {
        foreach (var page in _paginas.Keys)
        {
            if (_cfg.SitioDeUrl(page.Url) is null) continue;
            await EnviarEstadoAsync(page);
        }
    }

    private Task EnviarEstadoAsync(IPage page) => EnviarParaPaginaAsync(page, "estado", _env.EstadoAtual());

    /// <summary>Chama <c>window.__smsmaisIntegrador[canal](item)</c> no frame de cima da aba.</summary>
    private async Task EnviarParaPaginaAsync(IPage page, string canal, JsonObject item)
    {
        if (page.IsClosed) return;
        try
        {
            await page.MainFrame.EvaluateFunctionAsync(
                "(c, s) => { const w = window.__smsmaisIntegrador; if (w && typeof w[c] === 'function') w[c](JSON.parse(s)); }",
                canal, item.ToJsonString());
        }
        catch
        {
            // aba navegando/fechada — a próxima carga pede o estado de novo
        }
    }

    // ---------------------------------------------------------------- apoio

    private string? OperadorDe(IPage page) => _operadorPorPagina.TryGetValue(page, out var o) ? o : null;

    private static string? Primeiro(JsonObject campos, string chave) =>
        campos[chave] is JsonArray { Count: > 0 } a ? a[0]?.GetValue<string>() : null;

    private static string SoDigitos(string? s) => string.IsNullOrEmpty(s) ? string.Empty : new string(s.Where(char.IsDigit).ToArray());

    private static string Caminho(string url)
    {
        try { return new Uri(url).AbsolutePath; }
        catch { return url; }
    }

    private static string Resumir(string url) => url.Length > 80 ? url[..80] + "…" : url;

    /// <summary>O script de página (recurso embutido) precedido da configuração que ele precisa.</summary>
    private string MontarScript()
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream("pagina.js")
            ?? throw new InvalidOperationException("Recurso pagina.js não embutido.");
        using var leitor = new StreamReader(fluxo);
        var script = leitor.ReadToEnd();

        var sitios = new JsonArray();
        foreach (var s in _cfg.Sitios)
            sitios.Add(new JsonObject { ["id"] = s.Id, ["label"] = s.Label, ["host"] = s.Host, ["blur"] = s.Blur, ["modo"] = s.Modo });
        var cfg = new JsonObject
        {
            ["painelOrigin"] = _cfg.PainelOrigin,
            ["sitios"] = sitios,
            ["respostaMaxChars"] = _cfg.RespostaMaxChars,
        };
        return $"window.__smsmaisIntegradorCfg = {cfg.ToJsonString()};\n{script}";
    }
}

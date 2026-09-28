using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SMSMais.Integrador.Agente;

/// <summary>Sessão do painel do SMSMais, lida do <c>localStorage['smsmarica.auth']</c>.</summary>
public sealed record Sessao(string Token, DateTimeOffset ExpiraEm, string? UsuarioNome);

/// <summary>Marca da instituição (nome, cor, logo) — vem de <c>GET /publico/instituicao</c>.</summary>
public sealed record Marca(string NomeCurto, string CorPrimaria, string? LogoUrl);

/// <summary>
/// O "cérebro" que a extensão tem no service worker (<c>background.js</c>): guarda a sessão do
/// painel, acumula as capturas num buffer, envia em lote (gzip) para a API e mantém o estado do
/// LED. Formato do lote e dos itens é o MESMO da extensão — o backend não muda.
/// </summary>
public sealed class Enviador : IDisposable
{
    private readonly Config _cfg;
    private readonly Action<string> _log;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly List<JsonObject> _buffer = [];
    private readonly Lock _trava = new();
    private readonly string _installId;
    private readonly Timer _batimento;

    private Sessao? _sessao;
    private Marca _marca;
    private bool _enviando;
    private DateTime _ultimoEnvioOk = DateTime.MinValue;
    private bool _ultimaFalha;
    private DateTime _ultimaTentativa = DateTime.MinValue;

    /// <summary>Disparado sempre que o estado do LED/sessão muda (o observador difunde às páginas).</summary>
    public event Func<Task>? EstadoMudou;

    public Enviador(Config cfg, Action<string> log)
    {
        _cfg = cfg;
        _log = log;
        _marca = new Marca(cfg.MarcaNomePadrao, cfg.MarcaCorPadrao, null);
        _installId = LerOuCriarInstallId();
        // Batimento de fundo: o gatilho principal é oportunista (a cada captura).
        _batimento = new Timer(_ => _ = EnviarLoteAsync(), null, 30_000, 30_000);
    }

    public bool Autenticado => _sessao is not null && _sessao.ExpiraEm > DateTimeOffset.UtcNow;

    public int Pendentes
    {
        get { lock (_trava) return _buffer.Count; }
    }

    // ------------------------------------------------------------------ sessão

    public async Task DefinirSessaoAsync(Sessao? nova)
    {
        var mudou = nova?.Token != _sessao?.Token;
        _sessao = nova;
        if (mudou)
            _log(nova is null ? "Sessão do painel: NENHUMA (blur)." : $"Sessão do painel: {nova.UsuarioNome ?? "(sem nome)"} até {nova.ExpiraEm:HH:mm}.");
        await NotificarAsync();
        if (mudou && nova is not null) _ = BuscarMarcaAsync();
    }

    /// <summary>Lê a sessão no formato que o painel guarda (<c>token</c>, <c>expiraEm</c>, <c>usuario</c>).</summary>
    public static Sessao? LerSessao(JsonNode? no)
    {
        if (no is not JsonObject o) return null;
        var token = o["token"]?.GetValue<string>();
        var expira = o["expiraEm"]?.GetValue<string>();
        if (string.IsNullOrEmpty(token) || !DateTimeOffset.TryParse(expira, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var expiraEm))
            return null;
        if (expiraEm <= DateTimeOffset.UtcNow) return null;
        var usuario = o["usuario"] as JsonObject;
        var nome = usuario?["nome"]?.GetValue<string>() ?? usuario?["nomeCompleto"]?.GetValue<string>();
        return new Sessao(token, expiraEm, nome);
    }

    // ------------------------------------------------------------------- marca

    public async Task BuscarMarcaAsync()
    {
        try
        {
            using var resp = await _http.GetAsync($"{_cfg.ApiBase}/publico/instituicao");
            if (!resp.IsSuccessStatusCode) return;
            var i = JsonNode.Parse(await resp.Content.ReadAsStringAsync()) as JsonObject;
            if (i is null) return;
            var logoId = i["logoMidiaId"]?.GetValue<string>();
            _marca = new Marca(
                i["nomeCurto"]?.GetValue<string>() ?? _cfg.MarcaNomePadrao,
                i["corPrimaria"]?.GetValue<string>() ?? _cfg.MarcaCorPadrao,
                string.IsNullOrEmpty(logoId) ? null : $"{_cfg.ApiBase}/midias/{logoId}");
            await NotificarAsync();
        }
        catch
        {
            // API fora — segue com a marca padrão
        }
    }

    // ---------------------------------------------------------------- o estado

    /// <summary>Mesmo objeto que a extensão manda ao content script (<c>estadoAtual()</c>).</summary>
    public JsonObject EstadoAtual()
    {
        string estado;
        if (!Autenticado) estado = "desconectado";
        else if (_enviando) estado = "enviando";
        else if (_ultimaFalha) estado = "offline";
        else if ((DateTime.UtcNow - _ultimoEnvioOk).TotalMilliseconds < 1500) estado = "recebido";
        else estado = "conectado";

        var sitios = new JsonArray();
        foreach (var s in _cfg.Sitios)
            sitios.Add(new JsonObject { ["id"] = s.Id, ["label"] = s.Label, ["host"] = s.Host, ["blur"] = s.Blur, ["modo"] = s.Modo });

        return new JsonObject
        {
            ["auth"] = Autenticado,
            ["estado"] = estado,
            ["pendentes"] = Pendentes,
            ["usuario"] = _sessao?.UsuarioNome,
            ["marca"] = new JsonObject
            {
                ["nomeCurto"] = _marca.NomeCurto,
                ["corPrimaria"] = _marca.CorPrimaria,
                ["logoUrl"] = _marca.LogoUrl,
            },
            ["painelOrigin"] = _cfg.PainelOrigin,
            ["sitios"] = sitios,
        };
    }

    private Task NotificarAsync() => EstadoMudou?.Invoke() ?? Task.CompletedTask;

    // ---------------------------------------------------------------- captura

    public void Empilhar(JsonObject item)
    {
        lock (_trava)
        {
            _buffer.Add(item);
            if (_buffer.Count > _cfg.BufferMax)
                _buffer.RemoveRange(0, _buffer.Count - _cfg.BufferMax);
        }
    }

    /// <summary>Gatilho oportunista: tenta esvaziar a fila respeitando o intervalo mínimo.</summary>
    public void TalvezEnviar(bool forcar = false)
    {
        var agora = DateTime.UtcNow;
        if (!forcar && (agora - _ultimaTentativa).TotalMilliseconds < _cfg.LoteIntervaloMs) return;
        _ultimaTentativa = agora;
        _ = EnviarLoteAsync();
    }

    public async Task EnviarLoteAsync()
    {
        if (_enviando || !Autenticado) return;
        List<JsonObject> lote;
        lock (_trava)
        {
            if (_buffer.Count == 0) return;
            // Fatia respeitando o teto de itens e de bytes.
            var corte = 0;
            long bytes = 0;
            foreach (var ev in _buffer)
            {
                var t = ev.ToJsonString().Length;
                if (corte >= _cfg.LoteMaxItens || (corte > 0 && bytes + t > _cfg.LoteMaxBytes)) break;
                bytes += t;
                corte++;
            }
            lote = _buffer.GetRange(0, corte);
        }

        _enviando = true;
        await NotificarAsync();
        try
        {
            var corpo = JsonSerializer.Serialize(new
            {
                installId = _installId,
                versao = _cfg.Versao,
                enviadoEm = DateTime.UtcNow.ToString("o"),
                itens = lote,
            });

            using var conteudo = new ByteArrayContent(Gzip(corpo));
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            conteudo.Headers.ContentEncoding.Add("gzip");
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_cfg.ApiBase}{Config.RotaCapturas}") { Content = conteudo };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sessao!.Token);

            using var resp = await _http.SendAsync(req);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _log("API respondeu 401 — token venceu; volta a exigir login.");
                await DefinirSessaoAsync(null);
            }
            else if (resp.IsSuccessStatusCode)
            {
                lock (_trava) _buffer.RemoveRange(0, Math.Min(lote.Count, _buffer.Count));
                _ultimoEnvioOk = DateTime.UtcNow;
                _ultimaFalha = false;
                _log($"Lote enviado: {lote.Count} item(ns); {Pendentes} pendente(s).");
            }
            else
            {
                _ultimaFalha = true;
                _log($"API respondeu {(int)resp.StatusCode} — lote fica no buffer.");
            }
        }
        catch (Exception ex)
        {
            _ultimaFalha = true; // API fora do ar — segura no buffer e tenta depois
            _log($"API fora de alcance ({ex.GetType().Name}: {ex.Message}) — {Pendentes} no buffer.");
        }
        finally
        {
            _enviando = false;
            await NotificarAsync();
        }
    }

    private static byte[] Gzip(string texto)
    {
        using var saida = new MemoryStream();
        using (var gz = new GZipStream(saida, CompressionLevel.Fastest, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(texto);
            gz.Write(bytes, 0, bytes.Length);
        }
        return saida.ToArray();
    }

    // ----------------------------------------------------------- identificação

    private string LerOuCriarInstallId()
    {
        var caminho = Path.Combine(_cfg.PastaDados, "install.json");
        try
        {
            if (File.Exists(caminho))
            {
                var id = (JsonNode.Parse(File.ReadAllText(caminho)) as JsonObject)?["installId"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(id)) return id;
            }
        }
        catch
        {
            // arquivo corrompido — gera outro
        }
        var novo = Guid.NewGuid().ToString();
        Directory.CreateDirectory(_cfg.PastaDados);
        File.WriteAllText(caminho, new JsonObject { ["installId"] = novo }.ToJsonString());
        return novo;
    }

    public void Dispose()
    {
        _batimento.Dispose();
        _http.Dispose();
    }
}

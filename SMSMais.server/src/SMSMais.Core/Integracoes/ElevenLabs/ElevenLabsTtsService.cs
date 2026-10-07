using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Core.Integracoes.ElevenLabs;

/// <summary>Áudio sintetizado (OGG/Opus) ou o motivo de não ter vindo.</summary>
public sealed record SinteseResultado(byte[]? Audio, string? MimeType, string? Erro);

/// <summary>
/// Texto-para-fala (TTS) pela ElevenLabs. Devolve OGG/Opus (bom para nota de voz no WhatsApp).
/// Se não estiver configurada/ativa, devolve <see cref="SinteseResultado.Erro"/> em vez de lançar.
/// </summary>
public interface IElevenLabsTtsService
{
    Task<SinteseResultado> SintetizarAsync(string texto, CancellationToken ct = default);

    /// <summary>Vozes da conta (para o seletor na tela). Vazio se não configurada/sem acesso.</summary>
    Task<IReadOnlyList<VozElevenLabs>> ListarVozesAsync(CancellationToken ct = default);

    /// <summary>Vozes pt-BR da biblioteca pública, com filtros opcionais de gênero e idade.</summary>
    Task<IReadOnlyList<VozBibliotecaElevenLabs>> ListarBibliotecaPtBrAsync(
        string? genero, string? idade, string? busca, CancellationToken ct = default);

    /// <summary>Adiciona uma voz da biblioteca à conta. Devolve o voice_id na conta, ou null se falhar.</summary>
    Task<string?> AdicionarVozAsync(string publicOwnerId, string vozId, string nome, CancellationToken ct = default);
}

public sealed class ElevenLabsTtsService(
    HttpClient http,
    IElevenLabsConfigService config,
    ILogger<ElevenLabsTtsService> logger) : IElevenLabsTtsService
{
    // OGG/Opus 48 kHz — formato de nota de voz aceito pela Cloud API da Meta (audio/ogg = OPUS).
    private const string OutputFormat = "opus_48000_64";
    // Teto de texto por síntese: evita áudio gigante/custo alto num descuido.
    private const int MaxChars = 5000;

    public async Task<SinteseResultado> SintetizarAsync(string texto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto)) return new(null, null, "texto vazio");

        ElevenLabsContexto ctx;
        try { ctx = await config.ObterContextoAsync(ct); }
        catch (ValidacaoException ex) { return new(null, null, ex.Message); }

        var vozId = ctx.VozId;
        if (string.IsNullOrWhiteSpace(vozId))
        {
            vozId = await PrimeiraVozAsync(ctx, ct);
            if (string.IsNullOrWhiteSpace(vozId))
                return new(null, null, "nenhuma voz configurada e não achei voz na conta");
        }

        var corpo = texto.Length > MaxChars ? texto[..MaxChars] : texto;
        var url = $"{ctx.BaseUrl.TrimEnd('/')}/v1/text-to-speech/{Uri.EscapeDataString(vozId)}?output_format={OutputFormat}";
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                text = corpo,
                model_id = ctx.ModeloTts,
                voice_settings = new { speed = ctx.VelocidadeTts },
            });
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("xi-api-key", ctx.ApiKey);
            req.Headers.Accept.ParseAdd("audio/ogg");

            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("ElevenLabs TTS HTTP {Status}.", (int)resp.StatusCode);
                return new(null, null, $"HTTP {(int)resp.StatusCode}");
            }
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0) return new(null, null, "áudio vazio");
            return new(bytes, "audio/ogg", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha ao sintetizar voz no ElevenLabs.");
            return new(null, null, ex is TaskCanceledException ? "timeout no ElevenLabs" : ex.Message);
        }
    }

    public async Task<IReadOnlyList<VozElevenLabs>> ListarVozesAsync(CancellationToken ct = default)
    {
        ElevenLabsContexto ctx;
        try { ctx = await config.ObterContextoAsync(ct); }
        catch (ValidacaoException) { return []; }
        return await ListarVozesAsync(ctx, ct);
    }

    private async Task<IReadOnlyList<VozElevenLabs>> ListarVozesAsync(ElevenLabsContexto ctx, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{ctx.BaseUrl.TrimEnd('/')}/v1/voices");
            req.Headers.Add("xi-api-key", ctx.ApiKey);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return [];
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("voices", out var vs) || vs.ValueKind != JsonValueKind.Array)
                return [];
            var lista = new List<VozElevenLabs>();
            foreach (var v in vs.EnumerateArray())
            {
                if (!v.TryGetProperty("voice_id", out var id) || id.GetString() is not { Length: > 0 } vid) continue;
                var nome = v.TryGetProperty("name", out var n) ? n.GetString() ?? vid : vid;
                string? idioma = null, categoria = null;
                if (v.TryGetProperty("labels", out var lbl) && lbl.ValueKind == JsonValueKind.Object)
                    idioma = (lbl.TryGetProperty("language", out var lg) ? lg.GetString() : null)
                          ?? (lbl.TryGetProperty("accent", out var ac) ? ac.GetString() : null);
                if (v.TryGetProperty("category", out var cat)) categoria = cat.GetString();
                lista.Add(new VozElevenLabs(vid, nome, idioma, categoria));
            }
            return lista;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha ao listar vozes do ElevenLabs.");
            return [];
        }
    }

    /// <summary>Primeira voz da conta (quando o operador não fixou um voice_id).</summary>
    private async Task<string?> PrimeiraVozAsync(ElevenLabsContexto ctx, CancellationToken ct)
    {
        var vozes = await ListarVozesAsync(ctx, ct);
        return vozes.Count > 0 ? vozes[0].VozId : null;
    }

    private static readonly string[] GenerosValidos = ["male", "female"];
    private static readonly string[] IdadesValidas = ["young", "middle_aged", "old"];

    public async Task<IReadOnlyList<VozBibliotecaElevenLabs>> ListarBibliotecaPtBrAsync(
        string? genero, string? idade, string? busca, CancellationToken ct = default)
    {
        ElevenLabsContexto ctx;
        try { ctx = await config.ObterContextoAsync(ct); }
        catch (ValidacaoException) { return []; }

        var q = new List<string> { "language=pt", "page_size=50" };
        if (GenerosValidos.Contains(genero)) q.Add($"gender={genero}");
        if (IdadesValidas.Contains(idade)) q.Add($"age={idade}");
        if (!string.IsNullOrWhiteSpace(busca)) q.Add($"search={Uri.EscapeDataString(busca.Trim())}");
        var url = $"{ctx.BaseUrl.TrimEnd('/')}/v1/shared-voices?{string.Join('&', q)}";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("xi-api-key", ctx.ApiKey);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return [];
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("voices", out var vs) || vs.ValueKind != JsonValueKind.Array)
                return [];
            var lista = new List<VozBibliotecaElevenLabs>();
            foreach (var v in vs.EnumerateArray())
            {
                // Só português do Brasil: a biblioteca mistura pt-PT no language=pt.
                var locale = v.TryGetProperty("locale", out var lc) ? lc.GetString() : null;
                var accent = v.TryGetProperty("accent", out var ac) ? ac.GetString() : null;
                var ehBr = string.Equals(locale, "pt-BR", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(accent, "brazilian", StringComparison.OrdinalIgnoreCase);
                if (!ehBr) continue;
                if (!v.TryGetProperty("voice_id", out var idEl) || idEl.GetString() is not { Length: > 0 } vid) continue;
                var owner = v.TryGetProperty("public_owner_id", out var po) ? po.GetString() : null;
                if (string.IsNullOrEmpty(owner)) continue;
                lista.Add(new VozBibliotecaElevenLabs(
                    vid,
                    owner,
                    v.TryGetProperty("name", out var n) ? n.GetString() ?? vid : vid,
                    v.TryGetProperty("gender", out var g) ? g.GetString() : null,
                    v.TryGetProperty("age", out var a) ? a.GetString() : null,
                    v.TryGetProperty("preview_url", out var pv) ? pv.GetString() : null,
                    v.TryGetProperty("is_added_by_user", out var ad) && ad.ValueKind == JsonValueKind.True));
            }
            return lista;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha ao buscar vozes pt-BR na biblioteca do ElevenLabs.");
            return [];
        }
    }

    public async Task<string?> AdicionarVozAsync(string publicOwnerId, string vozId, string nome, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(publicOwnerId) || string.IsNullOrWhiteSpace(vozId)) return null;
        ElevenLabsContexto ctx;
        try { ctx = await config.ObterContextoAsync(ct); }
        catch (ValidacaoException) { return null; }

        var url = $"{ctx.BaseUrl.TrimEnd('/')}/v1/voices/add/{Uri.EscapeDataString(publicOwnerId)}/{Uri.EscapeDataString(vozId)}";
        try
        {
            var nomeLimpo = string.IsNullOrWhiteSpace(nome) ? vozId : nome.Trim();
            var payload = JsonSerializer.Serialize(new { new_name = nomeLimpo });
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("xi-api-key", ctx.ApiKey);
            using var resp = await http.SendAsync(req, ct);
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("ElevenLabs add voice HTTP {Status}.", (int)resp.StatusCode);
                return null;
            }
            using var doc = JsonDocument.Parse(corpo);
            // A conta pode manter o mesmo id; se vier outro, é o da cópia na conta.
            return doc.RootElement.TryGetProperty("voice_id", out var nv) && nv.GetString() is { Length: > 0 } novo
                ? novo : vozId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha ao adicionar voz da biblioteca do ElevenLabs.");
            return null;
        }
    }
}

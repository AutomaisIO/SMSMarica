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
    // Fala um pouco mais rápida que o natural. Faixa válida do ElevenLabs: 0.7 a 1.2.
    private const double Velocidade = 1.15;

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
                voice_settings = new { speed = Velocidade },
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

    /// <summary>Primeira voz da conta (quando o operador não fixou um voice_id).</summary>
    private async Task<string?> PrimeiraVozAsync(ElevenLabsContexto ctx, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{ctx.BaseUrl.TrimEnd('/')}/v1/voices");
            req.Headers.Add("xi-api-key", ctx.ApiKey);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("voices", out var vs) && vs.ValueKind == JsonValueKind.Array)
                foreach (var v in vs.EnumerateArray())
                    if (v.TryGetProperty("voice_id", out var id) && id.GetString() is { Length: > 0 } vid)
                        return vid;
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha ao listar vozes do ElevenLabs.");
            return null;
        }
    }
}

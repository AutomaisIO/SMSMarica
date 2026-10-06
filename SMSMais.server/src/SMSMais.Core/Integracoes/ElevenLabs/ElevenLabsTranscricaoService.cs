using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Core.Integracoes.ElevenLabs;

/// <summary>Resultado da transcrição: o texto, ou o motivo de não ter vindo.</summary>
public sealed record TranscricaoResultado(string? Texto, string? Erro);

/// <summary>
/// Fala-para-texto (STT) pela ElevenLabs (modelo Scribe). Resolve a credencial em
/// <see cref="IElevenLabsConfigService"/>; se não estiver configurada/ativa, devolve
/// <see cref="TranscricaoResultado.Erro"/> em vez de lançar — quem chama trata como falha.
/// </summary>
public interface IElevenLabsTranscricaoService
{
    Task<TranscricaoResultado> TranscreverAsync(byte[] audio, string? mimeType, CancellationToken ct = default);
}

public sealed class ElevenLabsTranscricaoService(
    HttpClient http,
    IElevenLabsConfigService config,
    ILogger<ElevenLabsTranscricaoService> logger) : IElevenLabsTranscricaoService
{
    public async Task<TranscricaoResultado> TranscreverAsync(byte[] audio, string? mimeType, CancellationToken ct = default)
    {
        if (audio is null || audio.Length == 0)
            return new(null, "áudio vazio");

        ElevenLabsContexto ctx;
        try { ctx = await config.ObterContextoAsync(ct); }
        catch (ValidacaoException ex) { return new(null, ex.Message); }

        var url = $"{ctx.BaseUrl.TrimEnd('/')}/v1/speech-to-text";
        try
        {
            using var form = new MultipartFormDataContent();
            var arquivo = new ByteArrayContent(audio);
            arquivo.Headers.ContentType = MediaTypeHeaderValue.Parse(
                string.IsNullOrWhiteSpace(mimeType) ? "audio/ogg" : mimeType);
            // O WhatsApp manda nota de voz em OGG/Opus; o nome só precisa de uma extensão plausível.
            form.Add(arquivo, "file", "audio.ogg");
            form.Add(new StringContent(ctx.Modelo), "model_id");
            // pt-BR: fixa o idioma melhora a precisão (ISO-639-3).
            form.Add(new StringContent("por"), "language_code");

            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
            req.Headers.Add("xi-api-key", ctx.ApiKey);

            using var resp = await http.SendAsync(req, ct);
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("ElevenLabs STT HTTP {Status}.", (int)resp.StatusCode);
                return new(null, $"HTTP {(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(corpo);
            var texto = doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(texto))
                return new(null, "transcrição vazia");
            return new(texto.Trim(), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha ao transcrever áudio no ElevenLabs.");
            return new(null, ex is TaskCanceledException ? "timeout no ElevenLabs" : ex.Message);
        }
    }
}

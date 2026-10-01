using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Tfd.Configuracao;

namespace SMSMais.Core.Conversas.Midias;

/// <summary>Arquivo baixado pelo Zap, ou o motivo de não ter vindo.</summary>
public sealed record MidiaBaixada(byte[]? Conteudo, string? MimeType, string? Erro, bool Definitivo);

/// <summary>
/// Baixa, pelo Automais.Zap, a mídia que o paciente mandou no WhatsApp. Só o relay tem o token da
/// Meta (ADR-0044); aqui vai o token do tenant, como no envio.
/// </summary>
public interface IZapMidiaCliente
{
    Task<MidiaBaixada> BaixarAsync(string mediaId, long tamanhoMaximo, CancellationToken ct = default);
}

public sealed class ZapMidiaCliente(
    HttpClient http,
    ITfdConfigService config,
    IConfiguration configuration) : IZapMidiaCliente
{
    public async Task<MidiaBaixada> BaixarAsync(string mediaId, long tamanhoMaximo, CancellationToken ct = default)
    {
        if (configuration.GetValue("Tfd:WhatsApp:Simular", defaultValue: false))
        {
            return new(null, null, "WhatsApp em modo simulado — não há mídia para baixar.", Definitivo: true);
        }

        TfdWhatsAppContexto ctx;
        try { ctx = await config.ObterWhatsAppContextoAsync(ct); }
        catch (ValidacaoException) { return new(null, null, "WhatsApp sem conta configurada.", Definitivo: false); }

        var url = $"{ctx.ZapBaseUrl.TrimEnd('/')}/v1/midias-recebidas/{Uri.EscapeDataString(mediaId)}"
            + $"?phone_number_id={Uri.EscapeDataString(ctx.PhoneNumberId)}&max_bytes={tamanhoMaximo}";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.ZapToken);
            using var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                return new(bytes, resp.Content.Headers.ContentType?.MediaType, null, Definitivo: false);
            }

            var corpo = await resp.Content.ReadAsStringAsync(ct);
            var erro = LerErro(corpo) ?? $"HTTP {(int)resp.StatusCode}";
            // 404 (expirou na Meta), 413 (grande demais), 400/403 (pedido errado): tentar de novo
            // não muda nada. 5xx e timeout podem ser o Zap reiniciando — vale outra tentativa.
            var definitivo = (int)resp.StatusCode is 400 or 403 or 404 or 413;
            return new(null, null, erro, definitivo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return new(null, null, ex is TaskCanceledException ? "timeout falando com o Zap" : ex.Message, Definitivo: false);
        }
    }

    private static string? LerErro(string corpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            return doc.RootElement.TryGetProperty("erro", out var e) ? e.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

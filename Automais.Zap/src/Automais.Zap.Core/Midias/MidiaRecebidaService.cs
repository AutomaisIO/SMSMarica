using System.Net.Http.Headers;
using System.Text.Json;
using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Tokens;
using Microsoft.Extensions.Logging;

namespace Automais.Zap.Core.Midias;

/// <summary>Arquivo que o cidadão mandou, baixado da Meta.</summary>
public sealed record MidiaRecebida(byte[] Conteudo, string MimeType, string? Sha256);

/// <summary>Resultado do download: o arquivo, ou o status HTTP + o motivo para devolver ao cliente.</summary>
public sealed record ResultadoMidiaRecebida(MidiaRecebida? Midia, int Status, string? Erro);

/// <summary>
/// Baixa da Meta a mídia (foto, PDF, áudio, vídeo) que um cidadão mandou para um número do tenant.
///
/// <para>Só o relay tem o token do System User, por isso o download é aqui (ADR-0044) — e
/// <b>não guarda nada</b>: devolve os bytes a quem pediu, como o envio devolve o wamid. Quem
/// decide se o arquivo fica é o sistema do cliente.</para>
///
/// <para>Dois passos na Graph: <c>GET /{media-id}</c> devolve uma URL assinada que expira em
/// minutos; <c>GET</c> nessa URL com o mesmo token devolve o arquivo. O <c>phone_number_id</c> vai
/// junto no primeiro passo para a Meta recusar mídia de outro número — um tenant não baixa o que
/// chegou para outro.</para>
/// </summary>
public interface IMidiaRecebidaService
{
    Task<ResultadoMidiaRecebida> BaixarAsync(
        ChamadorAutenticado chamador, string phoneNumberId, string mediaId, long tamanhoMaximo, CancellationToken ct = default);
}

public sealed class MidiaRecebidaService(
    HttpClient http,
    ITokenService tokens,
    IConfiguracaoMetaService configuracao,
    ILogger<MidiaRecebidaService> logger) : IMidiaRecebidaService
{
    public async Task<ResultadoMidiaRecebida> BaixarAsync(
        ChamadorAutenticado chamador, string phoneNumberId, string mediaId, long tamanhoMaximo, CancellationToken ct = default)
    {
        if (await tokens.AutorizarNumeroAsync(chamador, phoneNumberId, ct) is null)
        {
            logger.LogWarning("Token do tenant {Tenant} tentou baixar mídia pelo número {Numero}, que não é dele.",
                chamador.TenantNome, phoneNumberId);
            return new(null, 403, "Este token não pode usar esse número.");
        }

        if (string.IsNullOrWhiteSpace(mediaId) || !mediaId.All(char.IsDigit))
        {
            return new(null, 400, "media_id inválido.");
        }

        var creds = await configuracao.ObterAsync(ct);
        if (string.IsNullOrWhiteSpace(creds.TokenSistema))
        {
            return new(null, 503, "Plataforma sem credencial da Meta configurada.");
        }

        try
        {
            // 1) Metadados + URL assinada.
            var urlMeta = $"{creds.BaseUrl.TrimEnd('/')}/{mediaId}?phone_number_id={Uri.EscapeDataString(phoneNumberId)}";
            using var reqMeta = new HttpRequestMessage(HttpMethod.Get, urlMeta);
            reqMeta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.TokenSistema);
            using var respMeta = await http.SendAsync(reqMeta, ct);
            var texto = await respMeta.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(texto) ? "{}" : texto);
            var raiz = doc.RootElement;

            if (!respMeta.IsSuccessStatusCode)
            {
                var erro = raiz.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var m)
                    ? m.GetString()
                    : $"HTTP {(int)respMeta.StatusCode}";
                logger.LogWarning("Meta recusou os metadados da mídia {Media}: {Erro}", mediaId, erro);
                // 404 = expirou (a Meta guarda ~30 dias) ou não é deste número.
                return new(null, respMeta.StatusCode == System.Net.HttpStatusCode.NotFound ? 404 : 502, erro);
            }

            var url = Texto(raiz, "url");
            var mime = Texto(raiz, "mime_type") ?? "application/octet-stream";
            var sha = Texto(raiz, "sha256");
            if (raiz.TryGetProperty("file_size", out var fs) && fs.TryGetInt64(out var tamanho) && tamanho > tamanhoMaximo)
            {
                return new(null, 413, $"Arquivo de {tamanho / 1024 / 1024} MB, acima do limite.");
            }
            if (string.IsNullOrWhiteSpace(url))
            {
                return new(null, 502, "A Meta não devolveu o endereço da mídia.");
            }

            // 2) O arquivo. A URL só aceita o mesmo token e um User-Agent de navegador/servidor.
            using var reqArq = new HttpRequestMessage(HttpMethod.Get, url);
            reqArq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.TokenSistema);
            reqArq.Headers.UserAgent.ParseAdd("Automais.Zap/1.0");
            using var respArq = await http.SendAsync(reqArq, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!respArq.IsSuccessStatusCode)
            {
                logger.LogWarning("Download da mídia {Media} falhou: HTTP {Status}", mediaId, (int)respArq.StatusCode);
                return new(null, 502, $"Download na Meta falhou (HTTP {(int)respArq.StatusCode}).");
            }

            await using var fluxo = await respArq.Content.ReadAsStreamAsync(ct);
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            int lidos;
            while ((lidos = await fluxo.ReadAsync(buffer, ct)) > 0)
            {
                ms.Write(buffer, 0, lidos);
                if (ms.Length > tamanhoMaximo)
                {
                    return new(null, 413, "Arquivo acima do limite.");
                }
            }

            return new(new MidiaRecebida(ms.ToArray(), mime, sha), 200, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha baixando a mídia {Media}.", mediaId);
            return new(null, 502, ex is TaskCanceledException ? "timeout falando com a Meta" : "falha falando com a Meta");
        }
    }

    private static string? Texto(JsonElement e, string prop) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}

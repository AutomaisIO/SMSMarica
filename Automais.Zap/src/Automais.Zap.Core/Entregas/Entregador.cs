using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Automais.Zap.Core.Entregas;

public sealed class Entregador(HttpClient http, ILogger<Entregador> logger) : IEntregador
{
    public async Task<ResultadoEntrega> EntregarAsync(
        string url, byte[] corpo, string assinatura, string? segredoProprio = null, CancellationToken ct = default)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new ByteArrayContent(corpo);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            if (string.IsNullOrWhiteSpace(segredoProprio))
            {
                // Sem segredo próprio: repassa a assinatura da Meta. Só funciona enquanto os
                // dois lados usam o mesmo App Secret.
                req.Headers.TryAddWithoutValidation("X-Hub-Signature-256", assinatura);
            }
            else
            {
                var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                req.Headers.TryAddWithoutValidation(AssinaturaAutomais.CabecalhoTimestamp, ts.ToString());
                req.Headers.TryAddWithoutValidation(
                    AssinaturaAutomais.CabecalhoAssinatura, AssinaturaAutomais.Calcular(segredoProprio, ts, corpo));
            }

            using var resp = await http.SendAsync(req, ct);
            cronometro.Stop();

            var codigo = (int)resp.StatusCode;
            if (resp.IsSuccessStatusCode)
            {
                return new ResultadoEntrega(true, codigo, (int)cronometro.ElapsedMilliseconds, null);
            }

            logger.LogWarning("Entrega recusada por {Url}: HTTP {Codigo}.", url, codigo);
            return new ResultadoEntrega(false, codigo, (int)cronometro.ElapsedMilliseconds, $"HTTP {codigo}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            cronometro.Stop();
            logger.LogWarning(ex, "Falha entregando em {Url}.", url);
            var erro = ex is TaskCanceledException ? "timeout" : ex.Message;
            return new ResultadoEntrega(false, null, (int)cronometro.ElapsedMilliseconds, Truncar(erro, 500));
        }
    }

    private static string Truncar(string s, int max) => s.Length <= max ? s : s[..max];
}

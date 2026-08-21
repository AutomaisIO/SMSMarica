using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Tokens;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.Extensions.Logging;

namespace Automais.Zap.Core.Envio;

/// <summary>
/// Pedido de envio vindo do sistema do cliente. O corpo já vai no formato da Cloud API
/// (<c>type</c>, <c>text</c>, <c>template</c>…) — traduzir aqui só criaria uma segunda
/// gramática para manter em dia com a Meta.
/// </summary>
public sealed record PedidoEnvio(string PhoneNumberId, string Para, JsonElement Mensagem);

public sealed record ResultadoEnvio(bool Sucesso, string? Wamid, int? StatusHttp, string? Erro);

public interface IEnvioService
{
    Task<ResultadoEnvio> EnviarAsync(ChamadorAutenticado chamador, PedidoEnvio pedido, CancellationToken ct = default);
}

/// <summary>
/// Envia agora, direto para a Meta, e devolve o <c>wamid</c>.
///
/// <para><b>Não guarda a mensagem.</b> Não há fila nem agendamento aqui: quem agenda é o
/// sistema do cliente, que já tem a máquina de retentativa e o contexto clínico para decidir
/// quando enviar. É o que mantém a promessa de que o relay encaminha envelope e não retém
/// conteúdo — uma fila obrigaria a guardar o texto até a hora do disparo.</para>
///
/// <para>O que fica registrado é a mesma trilha operacional do inbound: linha nossa, tipo e
/// resultado. Sem texto, sem wamid, sem telefone de cidadão.</para>
/// </summary>
public sealed class EnvioService(
    HttpClient http,
    ZapDbContext db,
    ITokenService tokens,
    IConfiguracaoMetaService configuracao,
    TimeProvider relogio,
    ILogger<EnvioService> logger) : IEnvioService
{
    public async Task<ResultadoEnvio> EnviarAsync(
        ChamadorAutenticado chamador, PedidoEnvio pedido, CancellationToken ct = default)
    {
        var numero = await tokens.AutorizarNumeroAsync(chamador, pedido.PhoneNumberId, ct);
        if (numero is null)
        {
            logger.LogWarning("Token do tenant {Tenant} tentou enviar por {Numero}, que não é dele ou não está liberado.",
                chamador.TenantNome, pedido.PhoneNumberId);
            return new ResultadoEnvio(false, null, 403, "Este token não pode enviar por esse número.");
        }

        var creds = await configuracao.ObterAsync(ct);
        if (string.IsNullOrWhiteSpace(creds.TokenSistema))
        {
            logger.LogError("Sem token do System User — não dá para enviar.");
            return new ResultadoEnvio(false, null, 503, "Plataforma sem credencial de envio configurada.");
        }

        var corpo = MontarCorpo(pedido);
        var url = $"{creds.BaseUrl.TrimEnd('/')}/{pedido.PhoneNumberId}/messages";
        var cronometro = Stopwatch.StartNew();

        int? statusHttp = null;
        string? wamid = null;
        string? erro = null;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.TokenSistema);

            using var resp = await http.SendAsync(req, ct);
            statusHttp = (int)resp.StatusCode;
            var texto = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(texto) ? "{}" : texto);
            if (resp.IsSuccessStatusCode)
            {
                wamid = doc.RootElement.TryGetProperty("messages", out var ms)
                        && ms.ValueKind == JsonValueKind.Array && ms.GetArrayLength() > 0
                        && ms[0].TryGetProperty("id", out var idEl)
                    ? idEl.GetString()
                    : null;
            }
            else if (doc.RootElement.TryGetProperty("error", out var err))
            {
                erro = $"{Texto(err, "message")} (code {Texto(err, "code")})";
            }
            else
            {
                erro = $"HTTP {statusHttp}";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            erro = ex is TaskCanceledException ? "timeout falando com a Meta" : ex.Message;
            logger.LogWarning(ex, "Falha enviando por {Numero}.", pedido.PhoneNumberId);
        }
        finally
        {
            cronometro.Stop();
        }

        db.EntregasLog.Add(new EntregaLog
        {
            PhoneNumberId = pedido.PhoneNumberId,
            TenantId = chamador.TenantId,
            Tipo = "envio",
            Sucesso = erro is null,
            StatusHttp = statusHttp,
            DuracaoMs = (int)cronometro.ElapsedMilliseconds,
            Erro = erro is null ? null : Truncar(erro, 500),
            RecebidoEm = relogio.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);

        return erro is null
            ? new ResultadoEnvio(true, wamid, statusHttp, null)
            : new ResultadoEnvio(false, null, statusHttp, erro);
    }

    /// <summary>
    /// Monta o envelope da Cloud API em volta do que o cliente mandou. Os campos fixos
    /// (<c>messaging_product</c>, <c>to</c>) entram aqui para o chamador não ter de repetir —
    /// e para que ele não consiga trocar o destinatário por dentro do objeto.
    /// </summary>
    private static string MontarCorpo(PedidoEnvio pedido)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("messaging_product", "whatsapp");
            w.WriteString("recipient_type", "individual");
            w.WriteString("to", pedido.Para);

            foreach (var prop in pedido.Mensagem.EnumerateObject())
            {
                if (prop.NameEquals("messaging_product") || prop.NameEquals("to") || prop.NameEquals("recipient_type"))
                {
                    continue;
                }
                prop.WriteTo(w);
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string? Texto(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.ToString(),
                _ => null,
            }
            : null;

    private static string Truncar(string s, int max) => s.Length <= max ? s : s[..max];
}

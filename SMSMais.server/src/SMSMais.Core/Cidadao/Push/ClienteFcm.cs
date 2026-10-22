using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;

namespace SMSMais.Core.Cidadao.Push;

/// <summary>Uma notificação para UM aparelho. <see cref="Dados"/> só leva strings (regra do FCM).</summary>
public sealed record MensagemFcm(
    string Token,
    string Titulo,
    string Corpo,
    IReadOnlyDictionary<string, string> Dados);

/// <summary>Cliente do Firebase Cloud Messaging (API HTTP v1) com a conta de serviço do provedor <c>fcm</c>.</summary>
public interface IClienteFcm
{
    /// <summary>
    /// Access token OAuth2 da conta de serviço, guardado em cache até 5 minutos antes de vencer.
    /// <paramref name="renovar"/> ignora o cache (o "Testar" precisa provar a credencial agora).
    /// Lança <see cref="FalhaTokenFcmException"/> quando o Google recusa ou não responde.
    /// </summary>
    Task<string> ObterAccessTokenAsync(ContaServicoFcm conta, bool renovar = false, CancellationToken ct = default);

    /// <summary>
    /// Um POST <c>messages:send</c>. Não lança pela resposta do Google: devolve o desfecho já
    /// interpretado (inclusive timeout e falha de rede).
    /// </summary>
    Task<DesfechoEnvioFcm> EnviarAsync(
        ContaServicoFcm conta, string accessToken, MensagemFcm mensagem, CancellationToken ct = default);

    /// <summary>
    /// Um POST <c>messages:send</c> com <c>validate_only</c> para um tópico: o Firebase confere projeto,
    /// API ligada e permissão de envio da conta sem entregar nada a ninguém. É o que o "Testar" usa —
    /// o access token sozinho não prova que o envio passa. Não lança pela resposta do Google.
    /// </summary>
    Task<ValidacaoEnvioFcm> ValidarEnvioAsync(ContaServicoFcm conta, string accessToken, CancellationToken ct = default);
}

public sealed class ClienteFcm(HttpClient http, IMemoryCache cache) : IClienteFcm
{
    public const string UrlBase = "https://fcm.googleapis.com/v1";

    /// <summary>Canal criado pelo app no Android (prioridade alta — é o que faz aparecer na tela).</summary>
    public const string CanalAndroid = "avisos";

    /// <summary>
    /// Destino do envio de validação. Tópico, e não token, porque não depende de aparelho nenhum; e
    /// com <c>validate_only</c> nada é entregue mesmo que alguém um dia assine este nome.
    /// </summary>
    public const string TopicoValidacao = "teste-credencial";

    public async Task<string> ObterAccessTokenAsync(
        ContaServicoFcm conta, bool renovar = false, CancellationToken ct = default)
    {
        var chave = ChaveCache(conta);
        if (!renovar && cache.TryGetValue(chave, out string? emCache) && emCache is not null) return emCache;

        var assercao = AssinaturaContaServico.MontarAssercao(conta, DateTimeOffset.UtcNow);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assercao,
        });

        HttpResponseMessage resp;
        try
        {
            resp = await http.PostAsync(conta.TokenUri, form, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException
            || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new FalhaTokenFcmException(DesfechoEnvioFcm.DetalheIndisponivel,
                DesfechoEnvioFcm.Indisponivel($"token OAuth2: {ex.GetType().Name} {ex.Message}"));
        }

        using (resp)
        {
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                var codigo = (int)resp.StatusCode;
                var mensagemGoogle = MensagemErroOAuth(corpo) ?? $"HTTP {codigo}";
                var tecnico = $"token OAuth2 HTTP {codigo}: {mensagemGoogle}";
                throw resp.StatusCode == HttpStatusCode.TooManyRequests || codigo >= 500
                    ? new FalhaTokenFcmException(DesfechoEnvioFcm.DetalheIndisponivel, DesfechoEnvioFcm.Indisponivel(tecnico))
                    : new FalhaTokenFcmException(mensagemGoogle, DesfechoEnvioFcm.Credencial(tecnico));
            }

            var (accessToken, expiraEmSegundos) = LerToken(corpo);
            if (accessToken is null)
                throw new FalhaTokenFcmException("O Google respondeu sem access token.",
                    DesfechoEnvioFcm.Credencial("token OAuth2: resposta sem access_token"));

            var validade = TimeSpan.FromSeconds(expiraEmSegundos - 300);
            if (validade > TimeSpan.Zero) cache.Set(chave, accessToken, validade);
            return accessToken;
        }
    }

    public async Task<DesfechoEnvioFcm> EnviarAsync(
        ContaServicoFcm conta, string accessToken, MensagemFcm mensagem, CancellationToken ct = default)
    {
        using var req = RequisicaoEnvio(conta, accessToken, MontarCorpo(mensagem));

        DesfechoEnvioFcm desfecho;
        try
        {
            using var resp = await http.SendAsync(req, ct);
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            desfecho = InterpretadorRespostaFcm.Interpretar(resp.StatusCode, corpo);
        }
        catch (Exception ex) when (ex is HttpRequestException
            || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return DesfechoEnvioFcm.Indisponivel($"FCM: {ex.GetType().Name} {ex.Message}");
        }

        // Token de acesso recusado no meio da validade (chave revogada no Google): o próximo envio
        // pede outro em vez de insistir no do cache até ele vencer.
        if (desfecho.CredencialRecusada) cache.Remove(ChaveCache(conta));
        return desfecho;
    }

    public async Task<ValidacaoEnvioFcm> ValidarEnvioAsync(
        ContaServicoFcm conta, string accessToken, CancellationToken ct = default)
    {
        // Mesmo HttpClient do envio: mesmo timeout.
        using var req = RequisicaoEnvio(conta, accessToken, MontarCorpoValidacao());

        ValidacaoEnvioFcm validacao;
        HttpStatusCode status;
        try
        {
            using var resp = await http.SendAsync(req, ct);
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            status = resp.StatusCode;
            validacao = InterpretadorRespostaFcm.InterpretarValidacao(status, corpo, conta.ProjectId);
        }
        catch (Exception ex) when (ex is HttpRequestException
            || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return new ValidacaoEnvioFcm(false, DesfechoEnvioFcm.DetalheIndisponivel,
                $"FCM validate_only: {ex.GetType().Name} {ex.Message}");
        }

        // Como no envio: token recusado não fica no cache para o próximo envio de verdade.
        if (status is HttpStatusCode.Unauthorized) cache.Remove(ChaveCache(conta));
        return validacao;
    }

    private static HttpRequestMessage RequisicaoEnvio(ContaServicoFcm conta, string accessToken, JsonObject corpo)
    {
        var url = $"{UrlBase}/projects/{Uri.EscapeDataString(conta.ProjectId)}/messages:send";
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return req;
    }

    /// <summary>Corpo do envio de validação: <c>validate_only</c> + tópico, sem dado nenhum.</summary>
    internal static JsonObject MontarCorpoValidacao() => new()
    {
        ["validate_only"] = true,
        ["message"] = new JsonObject
        {
            ["topic"] = TopicoValidacao,
            ["notification"] = new JsonObject { ["title"] = "Teste", ["body"] = "Teste da credencial do servidor." },
        },
    };

    /// <summary>Corpo do <c>messages:send</c>. Exposto para o teste conferir o contrato byte a byte.</summary>
    internal static JsonObject MontarCorpo(MensagemFcm m)
    {
        var dados = new JsonObject();
        foreach (var (chave, valor) in m.Dados) dados[chave] = valor;

        return new JsonObject
        {
            ["message"] = new JsonObject
            {
                ["token"] = m.Token,
                ["notification"] = new JsonObject { ["title"] = m.Titulo, ["body"] = m.Corpo },
                ["data"] = dados,
                ["android"] = new JsonObject
                {
                    ["priority"] = "HIGH",
                    ["notification"] = new JsonObject { ["channel_id"] = CanalAndroid },
                },
                ["apns"] = new JsonObject
                {
                    ["headers"] = new JsonObject { ["apns-priority"] = "10" },
                    ["payload"] = new JsonObject { ["aps"] = new JsonObject { ["sound"] = "default" } },
                },
            },
        };
    }

    // Por credencial (hash do e-mail + chave): trocar o JSON em Integrações não reaproveita o
    // token da conta anterior.
    private static string ChaveCache(ContaServicoFcm conta) =>
        "fcm:access-token:" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{conta.ClientEmail}\n{conta.PrivateKeyId}\n{conta.PrivateKeyPem}")));

    private static (string? Token, int ExpiraEmSegundos) LerToken(string corpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            var r = doc.RootElement;
            var token = r.TryGetProperty("access_token", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;
            var expira = r.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var s) ? s : 3600;
            return (token, expira);
        }
        catch (JsonException)
        {
            return (null, 0);
        }
    }

    /// <summary><c>{"error":"invalid_grant","error_description":"Invalid JWT Signature."}</c> → texto legível.</summary>
    private static string? MensagemErroOAuth(string corpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;
            var erro = r.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            var descricao = r.TryGetProperty("error_description", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString()
                : null;
            return (erro, descricao) switch
            {
                (null, null) => null,
                (_, null) => erro,
                (null, _) => descricao,
                _ => $"{erro}: {descricao}",
            };
        }
        catch (JsonException)
        {
            return string.IsNullOrWhiteSpace(corpo) ? null : InterpretadorRespostaFcm.Encurtar(corpo, 200);
        }
    }
}

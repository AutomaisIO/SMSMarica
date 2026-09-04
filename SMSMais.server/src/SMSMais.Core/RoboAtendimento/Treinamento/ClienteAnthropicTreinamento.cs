using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data;

namespace SMSMais.Core.RoboAtendimento.Treinamento;

/// <summary>Uma chamada de ferramenta pedida pelo modelo.</summary>
public sealed record ChamadaFerramenta(string Id, string Nome, JsonElement Argumentos);

/// <summary>Resultado de um turno: os blocos crus (para ecoar), as chamadas e o uso.</summary>
public sealed record TurnoModelo(
    JsonElement Conteudo,
    IReadOnlyList<ChamadaFerramenta> Chamadas,
    string? StopReason,
    long TokensEntrada,
    long TokensSaida);

/// <summary>Uma ferramenta oferecida ao modelo.</summary>
public sealed record FerramentaModelo(string Nome, string Descricao, object InputSchema);

/// <summary>
/// Cliente da Messages API para o <b>agente treinador</b>. É separado do
/// <c>RoboAtendimentoMotorApi</c> de propósito: aquele é o motor do atendimento (Haiku, tool_choice
/// forçado, ferramenta terminal obrigatória, teto apertado) e não pode ganhar peso por causa do
/// treinamento.
///
/// Duas diferenças que vêm do modelo usado aqui (Fable 5.1):
/// <list type="bullet">
/// <item><b>Não existe tool_choice forçado</b> — <c>any</c>/<c>tool</c> devolvem 400. O loop usa
/// <c>auto</c> e a instrução nomeia a ferramenta terminal;</item>
/// <item><b>thinking é sempre ligado</b> — nada de <c>thinking</c>, <c>budget_tokens</c> ou
/// <c>temperature</c> no corpo (todos rejeitados). A profundidade se ajusta por
/// <c>output_config.effort</c>.</item>
/// </list>
/// Os blocos do assistente são ecoados <b>inteiros</b> a cada volta (inclusive os de raciocínio) —
/// o histórico é append-only, que é o que o modelo espera ao continuar o próprio turno.
/// </summary>
public sealed class ClienteAnthropicTreinamento(
    HttpClient http,
    SmsMaisDbContext db,
    IProtetorSegredos protetor,
    ILogger<ClienteAnthropicTreinamento> logger)
{
    public const string HttpClientName = "RoboTreinadorIa";

    private const string Endpoint = "v1/messages";
    private const string VersaoApi = "2023-06-01";
    private const int MaxTentativasHttp = 3;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Um turno. <paramref name="mensagens"/> é a lista viva da conversa (o chamador acrescenta o
    /// eco do assistente e os resultados das ferramentas).
    /// </summary>
    public async Task<TurnoModelo> ChamarAsync(
        string modelo,
        string instrucaoSistema,
        List<object> mensagens,
        IReadOnlyList<FerramentaModelo> ferramentas,
        string esforco,
        int maxTokens,
        CancellationToken ct)
    {
        var token = await ObterTokenAsync(ct)
            ?? throw new ValidacaoException("ia",
                "Token da Anthropic não configurado (Configuração da IA) — o treinamento do robô precisa dele.");

        var body = new Dictionary<string, object?>
        {
            ["model"] = modelo,
            ["max_tokens"] = maxTokens,
            // O briefing é grande e idêntico entre as fases e entre itens do mesmo dia: cachear
            // corta a maior fatia do custo de um ciclo de treinamento.
            ["system"] = new object[]
            {
                new
                {
                    type = "text",
                    text = instrucaoSistema,
                    cache_control = new { type = "ephemeral" },
                },
            },
            ["output_config"] = new { effort = esforco },
            ["messages"] = mensagens,
        };

        if (ferramentas.Count > 0)
        {
            body["tools"] = ferramentas
                .Select(object (f) => new
                {
                    name = f.Nome,
                    description = f.Descricao,
                    input_schema = f.InputSchema,
                    // Sem tool_choice forçado neste modelo, `strict` é o que garante que os
                    // argumentos cheguem válidos contra o schema.
                    strict = true,
                })
                .ToArray();
            body["tool_choice"] = new { type = "auto" };
        }

        using var doc = await EnviarAsync(token, body, ct);
        var raiz = doc.RootElement;

        var stop = raiz.TryGetProperty("stop_reason", out var s) ? s.GetString() : null;
        if (stop == "refusal")
        {
            var motivo = raiz.TryGetProperty("stop_details", out var sd)
                && sd.TryGetProperty("explanation", out var e) ? e.GetString() : null;
            throw new InvalidOperationException(
                "O modelo recusou a análise" + (string.IsNullOrWhiteSpace(motivo) ? "." : $": {motivo}"));
        }

        long entrada = 0, saida = 0;
        SomarUso(raiz, ref entrada, ref saida);

        var chamadas = new List<ChamadaFerramenta>();
        if (raiz.TryGetProperty("content", out var conteudo) && conteudo.ValueKind == JsonValueKind.Array)
        {
            foreach (var bloco in conteudo.EnumerateArray())
            {
                if (!bloco.TryGetProperty("type", out var t) || t.GetString() != "tool_use") continue;
                var nome = bloco.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                if (nome.Length == 0) continue;
                var id = bloco.TryGetProperty("id", out var i) ? i.GetString() ?? string.Empty : string.Empty;
                var args = bloco.TryGetProperty("input", out var inp) ? inp.Clone() : default;
                chamadas.Add(new ChamadaFerramenta(id, nome, args));
            }
        }

        return new TurnoModelo(
            conteudo.ValueKind == JsonValueKind.Array ? conteudo.Clone() : default,
            chamadas, stop, entrada, saida);
    }

    public static object ResultadoFerramenta(string id, string conteudo, bool erro = false) => new
    {
        type = "tool_result",
        tool_use_id = id,
        content = conteudo,
        is_error = erro,
    };

    private static void SomarUso(JsonElement raiz, ref long entrada, ref long saida)
    {
        if (!raiz.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object) return;
        entrada += Ler(u, "input_tokens") + Ler(u, "cache_read_input_tokens") + Ler(u, "cache_creation_input_tokens");
        saida += Ler(u, "output_tokens");

        static long Ler(JsonElement o, string campo) =>
            o.TryGetProperty(campo, out var v) && v.TryGetInt64(out var n) ? n : 0;
    }

    private async Task<JsonDocument> EnviarAsync(string token, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, Json);

        for (var tentativa = 1; ; tentativa++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("x-api-key", token);
            req.Headers.Add("anthropic-version", VersaoApi);

            using var resp = await http.SendAsync(req, ct);
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode) return JsonDocument.Parse(corpo);

            var transitorio = resp.StatusCode is HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                or (HttpStatusCode)529;
            if (!transitorio || tentativa >= MaxTentativasHttp)
                throw new InvalidOperationException($"Anthropic retornou {(int)resp.StatusCode}: {Truncar(corpo)}");

            var espera = TimeSpan.FromSeconds(Math.Pow(2, tentativa)); // 2s, 4s
            logger.LogWarning("Anthropic {Status} no treinamento (tentativa {N}) — reesperando {Espera}s.",
                (int)resp.StatusCode, tentativa, espera.TotalSeconds);
            await Task.Delay(espera, ct);
        }
    }

    private async Task<string?> ObterTokenAsync(CancellationToken ct)
    {
        var cfg = await db.IaConfiguracoes.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.TokenCifrado)) return null;
        try
        {
            return protetor.Revelar(cfg.TokenCifrado);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível revelar o token da Anthropic para o treinamento.");
            return null;
        }
    }

    private static string Truncar(string s) => s.Length <= 400 ? s : s[..400];
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data;

namespace SMSMais.Core.Translado.Geracao.IA;

/// <summary>Veículo disponível para a distribuição (capacidade já calculada em assentos livres).</summary>
public sealed record VeiculoIa(string VeiculoId, string Placa, int Capacidade, int AssentosAcompanhante);

/// <summary>Paciente a transportar (origem geocodificada + destino + necessidade de acompanhante).</summary>
public sealed record PacienteIa(
    string SessaoId, string Paciente, string UnidadeId, string Unidade,
    double OrigemLat, double OrigemLng, bool ComAcompanhante);

public sealed record AtribuicaoIa(string SessaoId, string VeiculoId);

public sealed record DistribuicaoIaResultado(IReadOnlyList<AtribuicaoIa> Atribuicoes, string? Justificativa);

/// <summary>
/// Distribui pacientes entre veículos usando o Claude (mesma config cifrada do módulo IA).
/// É uma sugestão: o motor de geração revalida capacidade/assentos no backend e cai para a
/// heurística determinística quando a IA não está configurada ou falha.
/// </summary>
public interface IDistribuidorIa
{
    /// <summary>Sugere a alocação paciente→veículo. Null se a IA não estiver configurada ou falhar.</summary>
    Task<DistribuicaoIaResultado?> DistribuirAsync(
        IReadOnlyList<PacienteIa> pacientes, IReadOnlyList<VeiculoIa> veiculos, CancellationToken ct = default);
}

public sealed class DistribuidorIa(
    HttpClient http,
    SmsMaisDbContext db,
    IProtetorSegredos protetor,
    ILogger<DistribuidorIa> logger) : IDistribuidorIa
{
    public const string HttpClientName = "IaProvedor";

    private const string EndpointMensagens = "v1/messages";
    private const string VersaoApi = "2023-06-01";
    private const string ModeloPadrao = "claude-opus-4-8";
    private const int MaxTokens = 4096;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<DistribuicaoIaResultado?> DistribuirAsync(
        IReadOnlyList<PacienteIa> pacientes, IReadOnlyList<VeiculoIa> veiculos, CancellationToken ct = default)
    {
        if (pacientes.Count == 0 || veiculos.Count == 0) return null;

        var cred = await ObterCredenciaisOuNuloAsync(ct);
        if (cred is null) return null;
        var (token, modelo) = cred.Value;

        var entrada = new
        {
            veiculos = veiculos.Select(v => new
            {
                v.VeiculoId,
                v.Placa,
                capacidade = v.Capacidade,
                assentosAcompanhante = v.AssentosAcompanhante,
            }),
            pacientes = pacientes.Select(p => new
            {
                p.SessaoId,
                p.Paciente,
                p.UnidadeId,
                p.Unidade,
                lat = p.OrigemLat,
                lng = p.OrigemLng,
                comAcompanhante = p.ComAcompanhante,
            }),
        };

        var body = new
        {
            model = modelo,
            max_tokens = MaxTokens,
            system = new object[]
            {
                new
                {
                    type = "text",
                    text = SystemPrompt,
                    cache_control = new { type = "ephemeral" },
                },
            },
            output_config = new
            {
                format = new { type = "json_schema", schema = Schema() },
            },
            messages = new object[]
            {
                new { role = "user", content = JsonSerializer.Serialize(entrada, JsonOpts) },
            },
        };

        try
        {
            using var doc = await EnviarAsync(token, body, ct);
            var texto = ExtrairTexto(doc.RootElement);
            var saida = JsonSerializer.Deserialize<Saida>(texto, JsonOpts);
            if (saida?.Atribuicoes is null) return null;

            var atribs = saida.Atribuicoes
                .Where(a => !string.IsNullOrWhiteSpace(a.SessaoId) && !string.IsNullOrWhiteSpace(a.VeiculoId))
                .Select(a => new AtribuicaoIa(a.SessaoId!, a.VeiculoId!))
                .ToList();
            return new DistribuicaoIaResultado(atribs, saida.Justificativa);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Distribuição via IA falhou; o motor usará a heurística.");
            return null;
        }
    }

    private async Task<JsonDocument> EnviarAsync(string token, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var req = new HttpRequestMessage(HttpMethod.Post, EndpointMensagens)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("x-api-key", token);
        req.Headers.Add("anthropic-version", VersaoApi);

        using var resp = await http.SendAsync(req, ct);
        var corpo = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Provedor de IA retornou {(int)resp.StatusCode}: {corpo}");
        }
        return JsonDocument.Parse(corpo);
    }

    private async Task<(string Token, string Modelo)?> ObterCredenciaisOuNuloAsync(CancellationToken ct)
    {
        var config = await db.IaConfiguracoes.AsNoTracking().FirstOrDefaultAsync(ct);
        if (config is null || string.IsNullOrWhiteSpace(config.TokenCifrado)) return null;

        try
        {
            var token = protetor.Revelar(config.TokenCifrado);
            var modelo = string.IsNullOrWhiteSpace(config.Modelo) ? ModeloPadrao : config.Modelo;
            return (token, modelo);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível revelar o token de IA para a distribuição.");
            return null;
        }
    }

    private static string ExtrairTexto(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Resposta da IA sem bloco de conteúdo.");
        }

        var sb = new StringBuilder();
        foreach (var bloco in content.EnumerateArray())
        {
            if (bloco.TryGetProperty("type", out var t) && t.GetString() == "text"
                && bloco.TryGetProperty("text", out var texto))
            {
                sb.Append(texto.GetString());
            }
        }
        var resultado = sb.ToString();
        if (string.IsNullOrWhiteSpace(resultado))
        {
            throw new InvalidOperationException("Resposta da IA sem texto.");
        }
        return resultado;
    }

    private const string SystemPrompt =
        "Você é um planejador de transporte sanitário (TFD — Tratamento Fora do Domicílio) da " +
        "Secretaria de Saúde de Maricá. Recebe a frota disponível e os pacientes a transportar no " +
        "dia e deve distribuir os pacientes nos veículos. Regras invioláveis:\n" +
        "- Cada paciente ocupa 1 assento; se 'comAcompanhante' for true, ocupa 2 assentos no mesmo veículo.\n" +
        "- NUNCA exceda a 'capacidade' (assentos livres) de um veículo.\n" +
        "- Um mesmo veículo deve atender pacientes do MESMO destino ('unidadeId'). Não misture destinos.\n" +
        "- Agrupe no mesmo veículo pacientes geograficamente próximos (use lat/lng) para encurtar a rota.\n" +
        "- Minimize a quantidade de veículos usados, sem violar capacidade nem misturar destinos.\n" +
        "- Se não houver assentos para todos, deixe de fora os pacientes excedentes (NÃO os inclua em " +
        "'atribuicoes'); prefira deixar de fora quem está mais isolado geograficamente.\n" +
        "- Use exatamente os 'sessaoId' e 'veiculoId' fornecidos. Não invente identificadores.\n" +
        "- 'justificativa' (pt-BR, curta) explica o raciocínio da distribuição.";

    private static object Schema() => new
    {
        type = "object",
        properties = new
        {
            atribuicoes = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        sessaoId = new { type = "string" },
                        veiculoId = new { type = "string" },
                    },
                    required = new[] { "sessaoId", "veiculoId" },
                    additionalProperties = false,
                },
            },
            justificativa = new { type = new[] { "string", "null" } },
        },
        required = new[] { "atribuicoes", "justificativa" },
        additionalProperties = false,
    };

    private sealed record Saida(
        [property: JsonPropertyName("atribuicoes")] List<AtribItem>? Atribuicoes,
        [property: JsonPropertyName("justificativa")] string? Justificativa);

    private sealed record AtribItem(
        [property: JsonPropertyName("sessaoId")] string? SessaoId,
        [property: JsonPropertyName("veiculoId")] string? VeiculoId);
}

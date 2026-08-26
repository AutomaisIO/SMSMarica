using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.RoboAtendimento.Runtime;

/// <summary>
/// Implementação HTTP do motor: chama o aiengine (Claude Code por assinatura) no kind
/// <c>atendimento</c>, endpoint síncrono <c>POST /internal/ai/atendimento/responder</c>.
/// Reusa o HttpClient "agente-ia" e a chave interna, como o IaChatController.
/// </summary>
public sealed class RoboAtendimentoMotorHttp(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<RoboAtendimentoMotorHttp> logger) : IRoboAtendimentoMotor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _baseUrl = (configuration["AgenteIa:BaseUrl"] ?? "http://127.0.0.1:5085").TrimEnd('/');
    private readonly string? _internalKey = configuration["AgenteIa:InternalKey"];

    public async Task<RespostaMotorRobo> ResponderAsync(EntradaMotorRobo entrada, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_internalKey))
            throw new InvalidOperationException("AgenteIa:InternalKey não configurada — motor do robô indisponível.");

        var corpo = new
        {
            chaveSessao = entrada.ChaveSessao,
            conversaId = entrada.ConversaId,
            pacienteId = entrada.PacienteId,
            assuntoId = entrada.AssuntoId,
            model = entrada.Modelo,
            systemPrompt = entrada.InstrucaoSistema,
            comandos = entrada.ComandosHabilitados,
            historico = entrada.Historico.Select(h => new { papel = h.Papel, texto = h.Texto }),
            mensagem = entrada.MensagemAtual,
            dentroDoHorario = entrada.DentroDoHorario,
        };

        var client = httpClientFactory.CreateClient("agente-ia");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/internal/ai/atendimento/responder")
        {
            Content = new StringContent(JsonSerializer.Serialize(corpo, Json), Encoding.UTF8, MediaTypeNames.Application.Json),
        };
        req.Headers.TryAddWithoutValidation("X-SMSMarica-Internal-Key", _internalKey.Trim());

        using var res = await client.SendAsync(req, ct);
        var payload = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            logger.LogWarning("Motor do robô respondeu {Status}: {Payload}", (int)res.StatusCode, Truncar(payload));
            throw new HttpRequestException($"Motor do robô retornou {(int)res.StatusCode}.");
        }

        var dto = JsonSerializer.Deserialize<RespostaMotorDto>(payload, Json)
            ?? throw new HttpRequestException("Motor do robô devolveu corpo vazio.");
        return new RespostaMotorRobo(
            dto.Texto?.Trim() ?? string.Empty,
            dto.HandOff,
            string.IsNullOrWhiteSpace(dto.MotivoHandOff) ? null : dto.MotivoHandOff,
            dto.Confianca,
            dto.TokensEntrada,
            dto.TokensSaida,
            dto.CustoUsd);
    }

    private static string Truncar(string s) => s.Length <= 300 ? s : s[..300];

    private sealed record RespostaMotorDto(
        [property: JsonPropertyName("texto")] string? Texto,
        [property: JsonPropertyName("handoff")] bool HandOff,
        [property: JsonPropertyName("motivoHandoff")] string? MotivoHandOff,
        [property: JsonPropertyName("confianca")] double? Confianca,
        [property: JsonPropertyName("tokensEntrada")] long? TokensEntrada,
        [property: JsonPropertyName("tokensSaida")] long? TokensSaida,
        [property: JsonPropertyName("custoUsd")] decimal? CustoUsd);
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace SMSMarica.Core.Integracoes.Proxy.Motores;

/// <summary>Constantes/utilidades compartilhadas pelos motores do Hub do Desenvolvedor.</summary>
internal static class HubDoDesenvolvedor
{
    public const string DefaultBaseUrl = "https://ws.hubdodesenvolvedor.com.br/v2/";

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Base URL efetiva: override em <c>ParametrosJson.baseUrl</c> ou o padrão.</summary>
    public static string BaseUrl(MotorExecucao cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.ParametrosJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(cfg.ParametrosJson);
                if (doc.RootElement.TryGetProperty("baseUrl", out var b) && b.ValueKind == JsonValueKind.String)
                {
                    var url = b.GetString();
                    if (!string.IsNullOrWhiteSpace(url)) return url.TrimEnd('/') + "/";
                }
            }
            catch (JsonException) { /* ParametrosJson malformado → usa o padrão */ }
        }
        return DefaultBaseUrl;
    }

    public static string Token(MotorExecucao cfg) =>
        string.IsNullOrWhiteSpace(cfg.Token)
            ? throw new MotorIndisponivelException(MotoresProxy.HubDoDesenvolvedor, "token não configurado")
            : cfg.Token.Trim();

    public sealed record CpfPayload(bool Status, string? Return, CpfPayloadResult? Result);

    public sealed record CpfPayloadResult(
        [property: JsonPropertyName("numero_de_cpf")] string? NumeroDeCpf,
        [property: JsonPropertyName("nome_da_pf")] string? NomeDaPf,
        [property: JsonPropertyName("data_nascimento")] string? DataNascimento,
        [property: JsonPropertyName("situacao_cadastral")] string? SituacaoCadastral);

    public sealed record CepPayload(bool Status, string? Return, CepPayloadResult? Result);

    public sealed record CepPayloadResult(
        string? Cep,
        string? Logradouro,
        string? Complemento,
        string? Bairro,
        string? Localidade,
        string? Uf,
        string? Ibge);
}

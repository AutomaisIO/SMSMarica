using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SMSMais.Core.Integracoes.Proxy.Motores;

/// <summary>Constantes/utilidades compartilhadas pelos motores do Hub do Desenvolvedor.</summary>
internal static class HubDoDesenvolvedor
{
    /// <summary>
    /// O Hub responde <c>status:false</c> tanto para NEGATIVA real (dados divergem/não
    /// encontrados) quanto para problema operacional DELE (sem saldo, instabilidade da fonte,
    /// token, "tente novamente"). Só é negativa autoritativa quando a mensagem diz
    /// explicitamente que os dados não conferem/não existem — todo o resto é tratado como
    /// indisponibilidade (retenta/fallback): nunca negar um cadastro por falha do fornecedor.
    /// </summary>
    public static bool EhNegativaAutoritativa(string? retorno, string? mensagem)
    {
        var texto = RemoverAcentos($"{retorno} {mensagem}").ToLowerInvariant();
        if (SinaisNegativa.Any(texto.Contains)) return true;

        // Casamento por FRASE EXATA é frágil: o Hub escreve "Data Nascimento invalida" (sem o
        // "de"), e a entrada "data de nascimento invalida" não casava — a negativa mais comum
        // do serviço caía em "indisponível" e disparava 3 tentativas + fallback ao SISREG a cada
        // consulta. Aqui o teste é por TERMOS: o assunto (cpf/data/nascimento) junto do defeito
        // (invalid/incorret), tolerando qualquer redação. Continua fora "token invalido" ou
        // "requisicao invalida", que não citam o assunto e são problema do fornecedor.
        var temAssunto = texto.Contains("nascimento") || texto.Contains("cpf") || texto.Contains("data");
        var temDefeito = texto.Contains("invalid") || texto.Contains("incorret");
        var ehDoFornecedor = texto.Contains("token") || texto.Contains("saldo")
            || texto.Contains("requisicao") || texto.Contains("credito");
        return temAssunto && temDefeito && !ehDoFornecedor;
    }

    private static readonly string[] SinaisNegativa =
    [
        "nao confere", "nao conferem", "divergente", "divergem",
        "nao encontrado", "nao localizado", "nao consta", "inexistente",
        "cpf invalido", "data de nascimento invalida", "cpf ou data",
        "cep invalido",
    ];

    private static string RemoverAcentos(string s)
    {
        var norm = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(norm.Length);
        foreach (var c in norm)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

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

    // "return" e "message" carregam o motivo cru do Hub quando status=false (varia por caso).
    public sealed record CpfPayload(bool Status, string? Return, string? Message, CpfPayloadResult? Result);

    public sealed record CpfPayloadResult(
        [property: JsonPropertyName("numero_de_cpf")] string? NumeroDeCpf,
        [property: JsonPropertyName("nome_da_pf")] string? NomeDaPf,
        [property: JsonPropertyName("data_nascimento")] string? DataNascimento,
        [property: JsonPropertyName("situacao_cadastral")] string? SituacaoCadastral,
        // O Hub pode mandar o sexo como "genero" ou "sexo" (varia por plano); aceitamos os dois.
        [property: JsonPropertyName("genero")] string? Genero = null,
        [property: JsonPropertyName("sexo")] string? Sexo = null);

    public sealed record CepPayload(bool Status, string? Return, string? Message, CepPayloadResult? Result);

    public sealed record CepPayloadResult(
        string? Cep,
        string? Logradouro,
        string? Complemento,
        string? Bairro,
        string? Localidade,
        string? Uf,
        string? Ibge);
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Dtos;

namespace SMSMarica.Core.Integracoes;

public sealed class HubConsultaService : IHubConsultaService
{
    public const string HttpClientName = "HubDoDesenvolvedor";

    private readonly HttpClient _http;
    private readonly string _token;
    private readonly ILogger<HubConsultaService> _logger;

    public HubConsultaService(HttpClient http, IConfiguration configuration, ILogger<HubConsultaService> logger)
    {
        _http = http;
        _logger = logger;
        _token = configuration["Integracoes:HubDoDesenvolvedor:Token"]
            ?? throw new InvalidOperationException("Configuração 'Integracoes:HubDoDesenvolvedor:Token' ausente.");
    }

    public async Task<HubCpfRespostaDto> ConsultarCpfAsync(
        string cpf,
        DateOnly dataNascimento,
        CancellationToken cancellationToken = default)
    {
        var cpfNormalizado = SoDigitos(cpf);
        if (cpfNormalizado.Length != 11)
        {
            throw new ValidacaoException("hub.cpf_invalido", "CPF deve ter 11 dígitos.");
        }

        var data = dataNascimento.ToString("dd/MM/yyyy");
        var url = $"cpf/?cpf={cpfNormalizado}&data={data}&token={_token}";

        try
        {
            using var resposta = await _http.GetAsync(url, cancellationToken);
            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hub CPF retornou {Status}.", resposta.StatusCode);
                throw new ConflitoException("hub.indisponivel", "Serviço de consulta CPF indisponível no momento.");
            }

            var payload = await resposta.Content.ReadFromJsonAsync<HubCpfPayload>(JsonOpts, cancellationToken);
            if (payload is null || !payload.Status || payload.Result is null)
            {
                throw new ValidacaoException(
                    "hub.cpf_nao_encontrado",
                    payload?.Return ?? "CPF/data de nascimento não conferem.");
            }

            return new HubCpfRespostaDto(
                Cpf: payload.Result.NumeroDeCpf ?? cpfNormalizado,
                Nome: payload.Result.NomeDaPf ?? string.Empty,
                DataNascimento: payload.Result.DataNascimento ?? data,
                SituacaoCadastral: payload.Result.SituacaoCadastral);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Falha de rede ao consultar Hub CPF.");
            throw new ConflitoException("hub.indisponivel", "Não foi possível alcançar o serviço de CPF.");
        }
    }

    public async Task<HubCepRespostaDto> ConsultarCepAsync(
        string cep,
        CancellationToken cancellationToken = default)
    {
        var cepNormalizado = SoDigitos(cep);
        if (cepNormalizado.Length != 8)
        {
            throw new ValidacaoException("hub.cep_invalido", "CEP deve ter 8 dígitos.");
        }

        var url = $"cep3/?cep={cepNormalizado}&token={_token}";

        try
        {
            using var resposta = await _http.GetAsync(url, cancellationToken);
            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hub CEP retornou {Status}.", resposta.StatusCode);
                throw new ConflitoException("hub.indisponivel", "Serviço de consulta CEP indisponível no momento.");
            }

            var payload = await resposta.Content.ReadFromJsonAsync<HubCepPayload>(JsonOpts, cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Localidade))
            {
                throw new NaoEncontradoException("Cep", cepNormalizado);
            }

            return new HubCepRespostaDto(
                Cep: payload.Cep ?? cepNormalizado,
                Logradouro: payload.Logradouro ?? string.Empty,
                Complemento: payload.Complemento,
                Bairro: payload.Bairro ?? string.Empty,
                Localidade: payload.Localidade,
                Uf: payload.Uf ?? string.Empty,
                Ibge: payload.Ibge);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Falha de rede ao consultar Hub CEP.");
            throw new ConflitoException("hub.indisponivel", "Não foi possível alcançar o serviço de CEP.");
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static string SoDigitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new([.. valor.Where(char.IsDigit)]);

    private sealed record HubCpfPayload(
        bool Status,
        string? Return,
        HubCpfPayloadResult? Result);

    private sealed record HubCpfPayloadResult(
        [property: JsonPropertyName("numero_de_cpf")] string? NumeroDeCpf,
        [property: JsonPropertyName("nome_da_pf")] string? NomeDaPf,
        [property: JsonPropertyName("data_nascimento")] string? DataNascimento,
        [property: JsonPropertyName("situacao_cadastral")] string? SituacaoCadastral);

    private sealed record HubCepPayload(
        string? Cep,
        string? Logradouro,
        string? Complemento,
        string? Bairro,
        string? Localidade,
        string? Uf,
        string? Ibge);
}

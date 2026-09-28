using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Telefonia.Dtos;

namespace SMSMais.Core.Telefonia;

/// <summary>
/// Cliente da API do Automais.Pabx (VM de telefonia). Só o que o SMSMais usa: ramais de
/// softphone com dono <c>smsmais</c>. Erros do Pabx viram as exceções tipadas daqui — 409 vira
/// <see cref="ConflitoException"/>, 400 vira <see cref="ValidacaoException"/>, fora do ar ou 5xx
/// vira <see cref="TelefoniaIndisponivelException"/>.
/// </summary>
public interface IPabxCliente
{
    bool Configurado { get; }

    Task CriarSoftphoneAsync(string ramal, string nomeExibicao, string descricao, Guid usuarioId, CancellationToken ct = default);
    Task AtualizarSoftphoneAsync(string ramal, string nomeExibicao, string descricao, bool ativo, CancellationToken ct = default);

    /// <summary>Remove o ramal do Pabx. Idempotente: ramal que já não existe não é erro.</summary>
    Task ExcluirAsync(string ramal, CancellationToken ct = default);

    Task<RamaisLivresDto> SugerirLivresAsync(int quantidade, CancellationToken ct = default);
    Task<CredencialSoftphoneDto> ObterCredencialAsync(string ramal, CancellationToken ct = default);
}

public sealed class PabxCliente(
    HttpClient http,
    IOptions<PabxOptions> options,
    ILogger<PabxCliente> logger) : IPabxCliente
{
    public const string DonoSistema = "smsmais";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PabxOptions _opcoes = options.Value;

    public bool Configurado => _opcoes.Configurado;

    public async Task CriarSoftphoneAsync(string ramal, string nomeExibicao, string descricao, Guid usuarioId, CancellationToken ct = default)
    {
        var corpo = new
        {
            numero = ramal,
            unidadeId = _opcoes.UnidadeSoftphone,
            descricao,
            callerId = nomeExibicao,
            tipo = "Softphone",
            donoSistema = DonoSistema,
            donoId = usuarioId.ToString(),
        };
        // A resposta traz o segredo em claro; não é lida nem guardada — o navegador busca a
        // credencial quando precisa.
        using var resposta = await EnviarAsync(HttpMethod.Post, "api/ramais", corpo, ct);
    }

    public async Task AtualizarSoftphoneAsync(string ramal, string nomeExibicao, string descricao, bool ativo, CancellationToken ct = default)
    {
        var corpo = new
        {
            unidadeId = _opcoes.UnidadeSoftphone,
            descricao,
            mac = (string?)null,
            marca = (string?)null,
            modelo = (string?)null,
            callerId = nomeExibicao,
            ativo,
        };
        using var resposta = await EnviarAsync(HttpMethod.Put, $"api/ramais/{Uri.EscapeDataString(ramal)}", corpo, ct);
    }

    public async Task ExcluirAsync(string ramal, CancellationToken ct = default)
    {
        try
        {
            using var resposta = await EnviarAsync(HttpMethod.Delete, $"api/ramais/{Uri.EscapeDataString(ramal)}", null, ct);
        }
        catch (NaoEncontradoException)
        {
            logger.LogInformation("Ramal {Ramal} já não existia no Pabx ao excluir.", ramal);
        }
    }

    public async Task<RamaisLivresDto> SugerirLivresAsync(int quantidade, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(HttpMethod.Get, $"api/ramais/faixas/livres?tipo=Softphone&quantidade={quantidade}", null, ct);
        return await resposta.Content.ReadFromJsonAsync<RamaisLivresDto>(Json, ct)
            ?? new RamaisLivresDto(null, null, []);
    }

    public async Task<CredencialSoftphoneDto> ObterCredencialAsync(string ramal, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(HttpMethod.Get, $"api/ramais/{Uri.EscapeDataString(ramal)}/credencial", null, ct);
        return await resposta.Content.ReadFromJsonAsync<CredencialSoftphoneDto>(Json, ct)
            ?? throw new TelefoniaIndisponivelException("A telefonia respondeu a credencial vazia.");
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string caminho, object? corpo, CancellationToken ct)
    {
        if (!Configurado)
            throw new TelefoniaIndisponivelException(
                "A telefonia não está configurada nesta instância (Telefonia:Pabx:BaseUrl / ApiKey).");

        using var requisicao = new HttpRequestMessage(metodo, new Uri(new Uri(_opcoes.BaseUrl), caminho));
        requisicao.Headers.Add("X-Api-Key", _opcoes.ApiKey);
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo, options: Json);

        HttpResponseMessage resposta;
        try
        {
            resposta = await http.SendAsync(requisicao, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new TelefoniaIndisponivelException("A telefonia não respondeu. Tente de novo em instantes.", ex);
        }

        if (resposta.IsSuccessStatusCode)
            return resposta;

        using (resposta)
        {
            var problema = await LerProblemaAsync(resposta, ct);
            switch (resposta.StatusCode)
            {
                case HttpStatusCode.Conflict:
                    throw new ConflitoException(problema.Codigo ?? "telefonia.conflito", problema.Mensagem ?? "Conflito na telefonia.");
                case HttpStatusCode.BadRequest:
                    throw new ValidacaoException("ramal", problema.Mensagem ?? "A telefonia recusou os dados do ramal.");
                case HttpStatusCode.NotFound:
                    throw new NaoEncontradoException("Ramal na telefonia", caminho);
                default:
                    logger.LogError("Pabx respondeu {Status} em {Metodo} {Caminho}: {Detalhe}",
                        (int)resposta.StatusCode, metodo, caminho, problema.Mensagem);
                    throw new TelefoniaIndisponivelException(
                        $"A telefonia respondeu erro {(int)resposta.StatusCode}. Tente de novo em instantes.");
            }
        }
    }

    private static async Task<(string? Codigo, string? Mensagem)> LerProblemaAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var raiz = doc.RootElement;
            var codigo = raiz.TryGetProperty("codigo", out var c) ? c.GetString() : null;

            // ValidationProblemDetails: a primeira mensagem de erro diz mais que o título genérico.
            if (raiz.TryGetProperty("errors", out var erros) && erros.ValueKind == JsonValueKind.Object)
            {
                foreach (var campo in erros.EnumerateObject())
                {
                    if (campo.Value.ValueKind == JsonValueKind.Array && campo.Value.GetArrayLength() > 0)
                        return (codigo, campo.Value[0].GetString());
                }
            }

            var detalhe = raiz.TryGetProperty("detail", out var d) ? d.GetString() : null;
            return (codigo, detalhe);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}

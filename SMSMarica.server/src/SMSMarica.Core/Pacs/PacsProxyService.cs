using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;

namespace SMSMarica.Core.Pacs;

public sealed class PacsProxyService : IPacsProxyService
{
    private readonly HttpClient _http;
    private readonly ILogger<PacsProxyService> _logger;

    /// <summary>
    /// Transfer-syntax preferido para frames (config <c>Pacs:Dcm4chee:TransferSyntaxPreferido</c>).
    /// Null por padrão = comportamento atual intacto. Quando setado, o proxy
    /// reescreve o Accept de requisições de frames para pedir esse transfer-syntax
    /// (ex.: um lossless como <c>1.2.840.10008.1.2.4.70</c>), o que pode reduzir o
    /// tamanho do payload do PACS sem perda de qualidade diagnóstica.
    /// </summary>
    private readonly string? _transferSyntaxPreferido;

    public PacsProxyService(
        HttpClient http,
        ILogger<PacsProxyService> logger,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _http = http;
        _logger = logger;
        _transferSyntaxPreferido = configuration["Pacs:Dcm4chee:TransferSyntaxPreferido"];
    }

    public async Task<HttpResponseMessage> EncaminharAsync(
        HttpMethod metodo,
        string caminho,
        string queryString,
        string? accept,
        CancellationToken cancellationToken = default)
    {
        var alvo = $"{caminho.TrimStart('/')}{queryString}";
        var requisicao = new HttpRequestMessage(metodo, alvo);

        // (D) Em requisições de frames, se houver transfer-syntax preferido configurado,
        // ajusta o Accept para preferi-lo. Default (null) mantém o Accept original.
        if (!string.IsNullOrWhiteSpace(_transferSyntaxPreferido)
            && caminho.Contains("/frames/", StringComparison.Ordinal))
        {
            accept = $"multipart/related; type=\"application/octet-stream\"; transfer-syntax={_transferSyntaxPreferido}";
        }

        if (!string.IsNullOrWhiteSpace(accept))
        {
            requisicao.Headers.TryAddWithoutValidation("Accept", accept);
        }

        try
        {
            return await _http.SendAsync(
                requisicao,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Falha de rede ao encaminhar para o PACS: {Alvo}", alvo);
            throw new ConflitoException("pacs.indisponivel", "Não foi possível alcançar o PACS.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout ao encaminhar para o PACS: {Alvo}", alvo);
            throw new ConflitoException("pacs.indisponivel", "O PACS demorou para responder.");
        }
    }
}

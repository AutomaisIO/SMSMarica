using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;

namespace SMSMarica.Core.Pacs;

public sealed class PacsProxyService : IPacsProxyService
{
    private readonly HttpClient _http;
    private readonly ILogger<PacsProxyService> _logger;

    public PacsProxyService(HttpClient http, ILogger<PacsProxyService> logger)
    {
        _http = http;
        _logger = logger;
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

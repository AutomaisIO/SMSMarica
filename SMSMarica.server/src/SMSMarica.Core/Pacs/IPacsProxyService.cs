namespace SMSMarica.Core.Pacs;

/// <summary>
/// Proxy transparente para o PACS dcm4chee (QIDO-RS + WADO-RS). Mantém o
/// endereço do PACS no servidor e evita o problema de CORS no browser.
/// BaseUrl fica em configuração (<c>Pacs:Dcm4chee:RsBaseUrl</c>).
/// </summary>
public interface IPacsProxyService
{
    /// <summary>
    /// Encaminha um GET RESTful para o PACS e devolve a resposta crua,
    /// sem ler o corpo (para permitir streaming pelo controller).
    /// </summary>
    /// <param name="caminho">Caminho relativo à base RS (ex.: <c>studies</c>, <c>studies/{uid}/series</c>).</param>
    /// <param name="queryString">Query string original, incluindo o <c>?</c> (ou vazia).</param>
    /// <param name="accept">Valor do header Accept a repassar (ou null).</param>
    Task<HttpResponseMessage> EncaminharAsync(
        string caminho,
        string queryString,
        string? accept,
        CancellationToken cancellationToken = default);
}

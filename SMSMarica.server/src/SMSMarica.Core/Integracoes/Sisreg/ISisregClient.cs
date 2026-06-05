using SMSMarica.Core.Integracoes.Sisreg.Dtos;

namespace SMSMarica.Core.Integracoes.Sisreg;

/// <summary>
/// Cliente de baixo nível para a API-SISREG (Elasticsearch, só-leitura). Resolve o índice
/// a partir da configuração e faz <c>POST /{indice}/_search</c> (o ES aceita POST igual ao
/// GET-com-body do manual). Não conhece regras de negócio — apenas executa a busca e projeta.
/// </summary>
public interface ISisregClient
{
    /// <summary>
    /// Executa uma busca no índice <paramref name="tipo"/> com o corpo Elasticsearch
    /// <paramref name="corpoConsulta"/> e projeta cada <c>_source</c> em <typeparamref name="T"/>.
    /// </summary>
    Task<SisregBuscaResultado<T>> BuscarAsync<T>(
        TipoIndiceSisreg tipo,
        object corpoConsulta,
        CancellationToken cancellationToken = default);
}

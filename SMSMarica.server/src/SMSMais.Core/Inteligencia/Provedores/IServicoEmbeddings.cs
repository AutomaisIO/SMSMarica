namespace SMSMais.Core.Inteligencia.Provedores;

/// <summary>
/// Gera embeddings de texto (provedor configurável — a Messages API não tem endpoint de
/// embeddings; default = Voyage AI). Dimensão deve casar com a coluna vector(1024).
/// </summary>
public interface IServicoEmbeddings
{
    Task<float[]> EmbeddarAsync(string texto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> EmbeddarLoteAsync(
        IReadOnlyList<string> textos, CancellationToken cancellationToken = default);
}

namespace SMSMais.Core.Pacs;

/// <summary>
/// Pré-aquecimento do cache de imagens de um estudo: lista séries/instâncias e
/// puxa o primeiro frame de cada instância pelo proxy, populando o
/// <see cref="IPacsCache"/>. Best-effort — falha de uma imagem não aborta as demais.
/// </summary>
public interface IPacsWarmupService
{
    /// <summary>Aquece o cache de todas as instâncias do estudo informado.</summary>
    Task AquecerEstudoAsync(string studyUid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aquece o cache do primeiro frame de UMA instância. Usado após invalidar o
    /// cache de uma imagem para reconstruí-la já quente. Best-effort. Com
    /// <paramref name="semCompressao"/>, aquece a variante CRUA (sentinela
    /// <c>?semCompressao=1</c>, sem JPEG-LS); sem ele, aquece conforme a config.
    /// </summary>
    Task AquecerInstanciaAsync(
        string studyUid, string seriesUid, string sopUid,
        bool semCompressao = false, CancellationToken cancellationToken = default);
}

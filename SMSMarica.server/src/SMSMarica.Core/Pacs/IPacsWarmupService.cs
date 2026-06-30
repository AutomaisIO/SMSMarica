namespace SMSMarica.Core.Pacs;

/// <summary>
/// Pré-aquecimento do cache de imagens de um estudo: lista séries/instâncias e
/// puxa o primeiro frame de cada instância pelo proxy, populando o
/// <see cref="IPacsCache"/>. Best-effort — falha de uma imagem não aborta as demais.
/// </summary>
public interface IPacsWarmupService
{
    /// <summary>Aquece o cache de todas as instâncias do estudo informado.</summary>
    Task AquecerEstudoAsync(string studyUid, CancellationToken cancellationToken = default);
}

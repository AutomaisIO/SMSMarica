namespace SMSMarica.Core.Laudos.Pdf;

public interface ILaudoPdfRenderer
{
    /// <summary>
    /// Gera o PDF do laudo identificado por <paramref name="laudoId"/>.
    /// Lança <see cref="Common.Excecoes.NaoEncontradoException"/> se não existir.
    /// </summary>
    Task<byte[]> GerarAsync(Guid laudoId, CancellationToken cancellationToken = default);
}

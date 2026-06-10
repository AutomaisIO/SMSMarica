namespace SMSMarica.Core.Laudos.Pdf;

public interface ILaudoPdfRenderer
{
    /// <summary>
    /// Gera o PDF do laudo identificado por <paramref name="laudoId"/>.
    /// Lança <see cref="Common.Excecoes.NaoEncontradoException"/> se não existir.
    /// </summary>
    /// <param name="incluirTarja">
    /// Quando <c>true</c> (default), inclui no rodapé a tarja "sem assinatura
    /// digital ICP-Brasil". Passar <c>false</c> ao preparar o PDF para
    /// assinatura digital — o documento assinado não pode declarar que não está
    /// assinado; o carimbo visual da assinatura entra depois.
    /// </param>
    Task<byte[]> GerarAsync(Guid laudoId, bool incluirTarja = true, CancellationToken cancellationToken = default);
}

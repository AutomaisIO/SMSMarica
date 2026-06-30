namespace SMSMarica.Core.Pacs;

/// <summary>
/// Resultado de uma transcodificação de frame pronta para ser servida ao
/// visualizador: o envelope <c>multipart/related</c> completo (com a parte
/// <c>image/jls</c>) e o Content-Type HTTP de topo correspondente.
/// </summary>
/// <param name="Conteudo">Bytes do envelope multipart (header + frame JPEG-LS + boundary de fechamento).</param>
/// <param name="ContentType">Content-Type HTTP de topo (multipart/related; type="image/jls"; boundary=...; transfer-syntax=...).</param>
public sealed record PacsFrameComprimido(byte[] Conteudo, string ContentType);

/// <summary>
/// Transcodifica um frame WADO-RS do PACS para JPEG-LS Lossless no NOSSO proxy
/// (o dcm4chee tem o compressor quebrado — HTTP 500). Atrás da flag
/// <c>Pacs:Compressao:Habilitado</c> (default false): com a flag desligada o
/// proxy mantém o caminho atual (frame cru ~53MB) 100% intacto.
/// </summary>
public interface IPacsTranscodeService
{
    /// <summary>Flag <c>Pacs:Compressao:Habilitado</c> (default false).</summary>
    bool Habilitado { get; }

    /// <summary>
    /// Discrimina a chave de cache da variante comprimida para NUNCA colidir com
    /// o frame cru já cacheado sob a mesma URL <c>/instances/.../frames/n</c>.
    /// Usado por proxy (GET) e pré-aquecimento, garantindo chaves idênticas.
    /// </summary>
    string DiscriminarCaminho(string caminho);

    /// <summary>
    /// Busca a instância completa do PACS, transcoda para JPEG-LS Lossless,
    /// extrai o frame e devolve o envelope multipart pronto. Qualquer falha
    /// (caminho não-frame, instância ausente, codec nativo indisponível, etc.)
    /// retorna <c>null</c> — o chamador cai no fallback do frame cru.
    /// </summary>
    Task<PacsFrameComprimido?> TranscodificarFrameAsync(string caminho, CancellationToken cancellationToken = default);
}

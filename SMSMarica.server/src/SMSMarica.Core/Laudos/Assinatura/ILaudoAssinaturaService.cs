using SMSMarica.Core.Laudos.Assinatura.Dtos;

namespace SMSMarica.Core.Laudos.Assinatura;

/// <summary>
/// Orquestra a assinatura digital de laudos. O médico clica "Assinar" (sessão web
/// autenticada) → geramos uma <b>chave de uso único</b> e o front lança o agente via
/// <c>automais-assinador://...?chave=</c>. O agente reivindica o job pela chave,
/// envia o certificado, recebe o hash, assina localmente e devolve a assinatura crua.
/// </summary>
public interface ILaudoAssinaturaService
{
    // ---- Fluxo do médico (JWT) ----

    /// <summary>Cria (ou reutiliza) o job e devolve a chave de uso único para o agente.</summary>
    Task<IniciarAssinaturaResultado> IniciarAsync(Guid laudoId, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Status da assinatura mais recente do laudo (para polling no front).</summary>
    Task<AssinaturaStatusDto> ObterStatusAsync(Guid laudoId, CancellationToken cancellationToken = default);

    /// <summary>PDF para download: o assinado (byte-estável) se houver, senão o gerado com tarja.</summary>
    Task<PdfDownloadDto> ObterPdfParaDownloadAsync(Guid laudoId, CancellationToken cancellationToken = default);

    /// <summary>True se o laudo tem assinatura concluída (para o cadeado).</summary>
    Task<bool> EstaAssinadoAsync(Guid laudoId, CancellationToken cancellationToken = default);

    /// <summary>Conjunto de laudoIds (entre os informados) com assinatura concluída.</summary>
    Task<IReadOnlySet<Guid>> QuaisAssinadosAsync(IReadOnlyCollection<Guid> laudoIds, CancellationToken cancellationToken = default);

    // ---- Fluxo do agente (autenticado pela chave, sem JWT/X-API-Key) ----

    /// <summary>Agente reivindica o job pela chave; recebe quem assinar (CPF) e o título.</summary>
    Task<ReivindicarResultado> ReivindicarAsync(string chave, CancellationToken cancellationToken = default);

    /// <summary>Agente envia o certificado; recebe o hash a assinar.</summary>
    Task<PrepararJobResultadoDto> PrepararAsync(
        string chave, IReadOnlyList<byte[]> cadeiaCertificado, CancellationToken cancellationToken = default);

    /// <summary>Agente envia a assinatura crua; o servidor embute o CMS e conclui.</summary>
    Task ConcluirAsync(string chave, byte[] rawSignature, CancellationToken cancellationToken = default);
}

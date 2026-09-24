using SMSMais.Core.Laudos.Assinatura.Dtos;

namespace SMSMais.Core.Laudos.Assinatura;

/// <summary>
/// Orquestra a assinatura digital de laudos. O médico clica "Assinar" (sessão web
/// autenticada) → geramos uma <b>chave de uso único</b> e o front lança o agente via
/// <c>automais-assinador://...?chave=</c>. O agente reivindica o job pela chave,
/// envia o certificado, recebe o hash, assina localmente e devolve a assinatura crua.
/// </summary>
public interface ILaudoAssinaturaService
{
    // ---- Fluxo do médico (JWT) ----

    /// <summary>
    /// PDF-base para o posicionamento do carimbo (modo PreparandoAssinatura, sem tarja/
    /// marca d'água). É o mesmo layout que será assinado — a médica posiciona o carimbo
    /// sobre ele antes de disparar a assinatura (ADR-0049).
    /// </summary>
    Task<byte[]> ObterPdfBaseAsync(Guid laudoId, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria (ou reutiliza) o job conforme o modo do médico (ADR-0061): Desktop devolve a chave
    /// de uso único para o agente; Nuvem devolve a URL de autorização; SemCertificado já
    /// carimba e deixa o documento aguardando a conferência. A
    /// <paramref name="posicao"/> (ADR-0049) fixa onde o carimbo será aplicado; quando
    /// nula, mantém o padrão legado (rodapé da última página).
    /// </summary>
    Task<IniciarAssinaturaResultado> IniciarAsync(
        Guid laudoId, Guid usuarioId, CarimboPosicaoDto? posicao, CancellationToken cancellationToken = default);

    /// <summary>Status da assinatura mais recente do laudo (para polling no front).</summary>
    Task<AssinaturaStatusDto> ObterStatusAsync(Guid laudoId, CancellationToken cancellationToken = default);

    /// <summary>PDF para download: o assinado (byte-estável) se houver, senão o gerado com tarja.</summary>
    Task<PdfDownloadDto> ObterPdfParaDownloadAsync(Guid laudoId, CancellationToken cancellationToken = default);

    /// <summary>PDF assinado que aguarda a conferência do médico (modal de aprovação).</summary>
    Task<byte[]> ObterPdfAprovacaoAsync(Guid laudoId, CancellationToken cancellationToken = default);

    /// <summary>Aprova o documento assinado: oficializa (Concluida) e avisa o paciente.</summary>
    Task AprovarAsync(Guid laudoId, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Rejeita na conferência: cancela (PDF preservado) e libera nova assinatura.</summary>
    Task RejeitarAsync(Guid laudoId, Guid usuarioId, CancellationToken cancellationToken = default);

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

    // ---- Fluxo em nuvem (retorno da IntegraICP, autenticado pelo state) ----

    /// <summary>
    /// Retorno da autorização em nuvem (ADR-0061): com a credencial aprovada pelo médico no
    /// app, busca o certificado, prepara o PAdES, assina o hash na IntegraICP, confere a
    /// assinatura com a chave pública e conclui (mesma trava de CPF do agente). Termina em
    /// <c>AguardandoAprovacao</c>, como os outros modos.
    /// </summary>
    Task ConcluirNuvemAsync(string state, string credencialId, CancellationToken cancellationToken = default);
}

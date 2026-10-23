using SMSMais.Core.ComunicacaoInterna.Dtos;

namespace SMSMais.Core.ComunicacaoInterna;

/// <summary>
/// Comunicação interna (ADR-0079): chat entre unidades e entre pessoas, dentro do painel. Toda
/// operação é do ponto de vista do usuário da requisição e só enxerga conversa de que ele participa
/// — conversa alheia responde 404, nunca 403 (não confirma que existe).
/// </summary>
public interface IComunicacaoInternaService
{
    Task<ContextoComunicacaoInternaDto> ObterContextoAsync(CancellationToken ct = default);

    Task<ResumoComunicacaoInternaDto> ObterResumoAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ConversaInternaItemDto>> ListarConversasAsync(CancellationToken ct = default);

    Task<ConversaInternaDetalheDto> ObterConversaAsync(Guid conversaId, CancellationToken ct = default);

    /// <summary>Abre (ou reabre — é única por par) a conversa entre a minha unidade e a de destino.</summary>
    Task<ConversaInternaItemDto> AbrirConversaUnidadeAsync(AbrirConversaUnidadeRequest request, CancellationToken ct = default);

    /// <summary>Abre (ou reabre — é única por par) a conversa direta com outra pessoa.</summary>
    Task<ConversaInternaItemDto> AbrirConversaDiretaAsync(AbrirConversaDiretaRequest request, CancellationToken ct = default);

    Task<PaginaMensagensInternasDto> ListarMensagensAsync(
        Guid conversaId, DateTime? antesDe, int limite, CancellationToken ct = default);

    Task<MensagemInternaDto> EnviarAsync(
        Guid conversaId, string? texto, AnexoEnviadoInterno? anexo, CancellationToken ct = default);

    Task MarcarLidaAsync(Guid conversaId, CancellationToken ct = default);

    Task SilenciarAsync(Guid conversaId, bool silenciada, CancellationToken ct = default);

    Task<ConteudoAnexoInterno> ObterAnexoAsync(Guid mensagemId, CancellationToken ct = default);

    Task<IReadOnlyList<UnidadeInternaDto>> ListarUnidadesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<UsuarioInternoDto>> ListarUsuariosDaUnidadeAsync(Guid unidadeId, CancellationToken ct = default);

    Task<IReadOnlyList<ResultadoBuscaInternaDto>> BuscarAsync(string? termo, CancellationToken ct = default);

    /// <summary>
    /// Para o "digitando…" do hub (que não tem a requisição HTTP, por isso o usuário vem
    /// explícito): o nome de quem digita e a audiência da conversa, ou <c>null</c> se ele não
    /// participa dela.
    /// </summary>
    Task<(string Nome, AudienciaInterna Audiencia)?> ObterAudienciaDigitandoAsync(
        Guid usuarioId, Guid conversaId, CancellationToken ct = default);
}

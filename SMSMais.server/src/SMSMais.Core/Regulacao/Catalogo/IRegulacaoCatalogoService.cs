using SMSMais.Core.Regulacao.Catalogo.Dtos;

namespace SMSMais.Core.Regulacao.Catalogo;

/// <summary>
/// Mantém o catálogo canônico de procedimentos a partir dos catálogos espelhados do SISREG, do
/// SER e do SERNIT, e guarda a curadoria do pareamento entre sistemas (ADR-0052).
/// </summary>
public interface IRegulacaoCatalogoService
{
    /// <summary>
    /// Relê as três origens, cria o que é novo, desativa o que sumiu, gera os embeddings que
    /// faltam e propõe pareamentos. É idempotente: rodar duas vezes seguidas não muda nada.
    /// </summary>
    Task<RegulacaoCatalogoSyncResultadoDto> SincronizarAsync(CancellationToken ct);

    Task<IReadOnlyList<RegulacaoSugestaoPareamentoDto>> ListarSugestoesAsync(CancellationToken ct);

    /// <summary>
    /// Move a origem para <paramref name="procedimentoId"/> e marca o vínculo como confirmado —
    /// a partir daí o sync não mexe mais nela. Canônico que ficar sem origem ativa é desativado.
    /// </summary>
    Task ConfirmarPareamentoAsync(Guid origemId, Guid procedimentoId, CancellationToken ct);

    /// <summary>Descarta a sugestão sem mover nada; o canônico atual continua valendo.</summary>
    Task RejeitarPareamentoAsync(Guid origemId, CancellationToken ct);

    Task RenomearCanonicoAsync(Guid procedimentoId, string nome, CancellationToken ct);
}

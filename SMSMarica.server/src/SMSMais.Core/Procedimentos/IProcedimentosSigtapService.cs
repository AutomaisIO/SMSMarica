using SMSMais.Core.Procedimentos.Dtos;

namespace SMSMais.Core.Procedimentos;

public interface IProcedimentosSigtapService
{
    /// <summary>
    /// Busca paginada por código (prefixo) ou nome (contains, case-insensitive).
    /// Default: lista os 50 mais relevantes — útil para popular dropdown.
    /// </summary>
    Task<IReadOnlyList<ProcedimentoSigtapDto>> ListarAsync(
        string? busca,
        string? grupo,
        int limite = 50,
        CancellationToken cancellationToken = default);

    Task<ProcedimentoSigtapDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
}

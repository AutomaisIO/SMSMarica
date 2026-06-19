using SMSMarica.Core.Faturamento.Dtos;

namespace SMSMarica.Core.Faturamento;

/// <summary>
/// Faturamento SUS/BPA do transporte (FT10). Calcula unidades de forma <b>proporcional</b>
/// (km a bordo ÷ km-por-unidade) com valor e código SIGTAP configuráveis, e gera relatórios
/// por paciente/motorista/veículo/tipo de tratamento/unidade num período.
/// </summary>
public interface IFaturamentoService
{
    /// <summary>Contabiliza (cria/atualiza) o registro de faturamento de uma sessão realizada.</summary>
    Task<RegistroFaturamentoDto> ContabilizarAsync(Guid sessaoId, CancellationToken ct = default);

    Task<IReadOnlyList<RegistroFaturamentoDto>> ListarAsync(
        int? competencia, DateOnly? de, DateOnly? ate, CancellationToken ct = default);

    Task<ResumoFaturamentoDto> ResumoAsync(
        DimensaoFaturamento dimensao, int? competencia, DateOnly? de, DateOnly? ate, CancellationToken ct = default);

    Task<TfdConfigFaturamentoDto> ObterConfigAsync(CancellationToken ct = default);
    Task AtualizarConfigAsync(AtualizarTfdConfigFaturamentoRequest request, CancellationToken ct = default);
}

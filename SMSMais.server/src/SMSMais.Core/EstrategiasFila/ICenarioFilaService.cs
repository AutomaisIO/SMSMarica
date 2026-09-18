using SMSMais.Core.EstrategiasFila.Dtos;

namespace SMSMais.Core.EstrategiasFila;

/// <summary>
/// O retrato de hoje de um procedimento: fila, ritmo de entrada, vazão real, oferta publicada e
/// quanto dela é usada. É o ponto de partida de toda simulação — e é <b>determinístico</b>: dois
/// operadores que abrem o mesmo procedimento no mesmo minuto veem o mesmo número.
/// </summary>
public interface ICenarioFilaService
{
    /// <summary>
    /// Os procedimentos que se pode simular, com o tamanho da fila ao lado do nome: tudo o que
    /// tem escala vigente no SISREG mais tudo o que tem gente esperando (mesmo sem escala).
    /// </summary>
    /// <param name="ordenar"><c>fila</c> (padrão), <c>espera</c> ou <c>nome</c>.</param>
    Task<IReadOnlyList<ProcedimentoComFilaDto>> ListarProcedimentosAsync(
        string? busca, string? ordenar, CancellationToken ct = default);

    /// <summary>Monta o cenário atual do procedimento (família grupo↔item resolvida).</summary>
    Task<CenarioFilaDto> MontarAsync(string? procedimentoCodigo, string procedimentoNome, CancellationToken ct = default);
}

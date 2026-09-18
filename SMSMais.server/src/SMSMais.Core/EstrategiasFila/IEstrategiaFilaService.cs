using SMSMais.Core.EstrategiasFila.Dtos;

namespace SMSMais.Core.EstrategiasFila;

/// <summary>
/// Estratégias de fila: simular, salvar, reabrir, rerodar, comparar (ADR-0058).
/// <b>Nada aqui escreve no SISREG nem em sistema externo</b> — a estratégia fica no nosso banco
/// para consulta; "aplicada" é anotação humana.
/// </summary>
public interface IEstrategiaFilaService
{
    /// <summary>Cenário + projeção com os parâmetros informados. Não grava nada.</summary>
    Task<SimularRespostaDto> SimularAsync(SimularRequest request, CancellationToken ct = default);

    /// <summary>Só a projeção, sem remontar o cenário — para a tela reagir a cada clique no quadro.</summary>
    ProjecaoDto Projetar(ProjetarRequest request);

    Task<IReadOnlyList<EstrategiaResumoDto>> ListarAsync(EstrategiaFiltro filtro, CancellationToken ct = default);

    Task<EstrategiaDto> ObterAsync(Guid id, CancellationToken ct = default);

    /// <summary>Cria a estratégia e grava a rodada 1 (manual) com o cenário de agora.</summary>
    Task<Guid> CriarAsync(CriarEstrategiaRequest request, CancellationToken ct = default);

    Task AtualizarAsync(Guid id, AtualizarEstrategiaRequest request, CancellationToken ct = default);

    /// <summary>Nova rodada — manual (só simula) ou agente (chama a IA). Sempre append.</summary>
    Task<RodadaDto> RodarAsync(Guid id, NovaRodadaRequest request, CancellationToken ct = default);

    Task<RodadaDto> ObterRodadaAsync(Guid id, int numero, CancellationToken ct = default);

    Task MarcarAplicadaAsync(Guid id, MarcarAplicadaRequest request, CancellationToken ct = default);

    Task ArquivarAsync(Guid id, CancellationToken ct = default);

    Task ExcluirAsync(Guid id, CancellationToken ct = default);
}

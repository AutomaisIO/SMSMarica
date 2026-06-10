using SMSMarica.Core.Integracoes.Pep.Background;
using SMSMarica.Core.Integracoes.Pep.Dtos;

namespace SMSMarica.Core.Integracoes.Pep;

/// <summary>
/// Orquestra a importação de bases de PEP (Salux e futuros) para o hub FHIR: lista as bases
/// candidatas (IaFontes), dispara um job em background, expõe status/progresso e histórico.
/// </summary>
public interface IPepSincronizacaoService
{
    /// <summary>Bases disponíveis para importação (com flag de suporte por tipo de PEP).</summary>
    Task<IReadOnlyList<BasePepDto>> ListarBasesAsync(CancellationToken ct = default);

    /// <summary>Valida e enfileira uma importação. Devolve o id da execução criada.</summary>
    Task<Guid> IniciarAsync(IniciarImportacaoRequest request, CancellationToken ct = default);

    /// <summary>Solicita a parada da importação em andamento (cancelamento cooperativo).</summary>
    Task CancelarAsync(CancellationToken ct = default);

    /// <summary>Status do run vivo (se houver) ou da última execução persistida.</summary>
    Task<StatusImportacaoDto> ObterStatusAsync(CancellationToken ct = default);

    /// <summary>Histórico de execuções (mais recentes primeiro), opcionalmente por base.</summary>
    Task<IReadOnlyList<ExecucaoImportacaoDto>> ListarExecucoesAsync(Guid? fonteId = null, CancellationToken ct = default);

    /// <summary>
    /// Falhas duráveis de importação (mais recentes primeiro). Filtra por execução e/ou base e,
    /// com <paramref name="somentePendentes"/>, só as ainda não resolvidas — insumo do reimport por cd.
    /// </summary>
    Task<IReadOnlyList<FalhaImportacaoDto>> ListarFalhasAsync(
        Guid? execucaoId = null, Guid? fonteId = null, bool somentePendentes = false, CancellationToken ct = default);

    /// <summary>Executa um job (chamado pelo runner em background). Não lança — registra o erro na execução.</summary>
    Task ExecutarAsync(PepImportacaoJob job, CancellationToken ct = default);
}

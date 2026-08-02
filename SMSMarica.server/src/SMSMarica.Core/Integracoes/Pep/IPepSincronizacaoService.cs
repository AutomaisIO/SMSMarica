using SMSMarica.Core.Integracoes.Pep.Background;
using SMSMarica.Core.Integracoes.Pep.Divergencias;
using SMSMarica.Core.Integracoes.Pep.Dtos;
using SMSMarica.Data.Entities.Enums;

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

    /// <summary>
    /// Enfileira uma execução INCREMENTAL disparada pelo scheduler (ADR-0024). Ao contrário
    /// de <see cref="IniciarAsync"/>, não lança em colisão/indisponibilidade — devolve
    /// <c>null</c> e deixa o scheduler reprogramar (corrida não é erro).
    /// </summary>
    Task<Guid?> IniciarAgendadoAsync(Guid fonteId, bool forcarMedicos, CancellationToken ct = default);

    /// <summary>Agendas do sincronismo contínuo (uma por base), com estado de backoff.</summary>
    Task<IReadOnlyList<AgendaPepDto>> ListarAgendasAsync(CancellationToken ct = default);

    /// <summary>Cria/atualiza a agenda de uma base.</summary>
    Task<AgendaPepDto> SalvarAgendaAsync(SalvarAgendaPepRequest request, CancellationToken ct = default);

    /// <summary>
    /// Diagnóstico origem×hub de uma base: marcas d'água + pendências na origem desde cada
    /// marca + contagens do hub. Abre uma conexão Oracle pontual (read-only).
    /// </summary>
    Task<DiagnosticoPepDto> ObterDiagnosticoAsync(Guid fonteId, CancellationToken ct = default);

    /// <summary>
    /// Para a importação em andamento (cancelamento cooperativo) e, com
    /// <paramref name="pausarHoras"/>, também PAUSA o motor — sem isso o scheduler religa
    /// sozinho no próximo intervalo e "parar" não serve de backout.
    /// </summary>
    Task CancelarAsync(int? pausarHoras = null, CancellationToken ct = default);

    /// <summary>Pausa (horas &gt; 0) ou retoma (null/0) o motor de uma base.</summary>
    Task<AgendaPepDto> PausarMotorAsync(Guid fonteId, int? horas, CancellationToken ct = default);

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

    /// <summary>
    /// Relatório de divergências de identidade origem×hub (mesmo CPF, dado de identidade
    /// diferente). Ver <c>docs/integracoes/plano-sincronismo-hub.md §3.2</c>.
    /// </summary>
    Task<IReadOnlyList<DivergenciaIdentidadeDto>> ListarDivergenciasAsync(
        Guid? fonteId = null, StatusDivergenciaIdentidade? status = null, CancellationToken ct = default);

    /// <summary>Contadores do relatório de divergências (cabeçalho da tela).</summary>
    Task<ResumoDivergenciasDto> ResumoDivergenciasAsync(Guid? fonteId = null, CancellationToken ct = default);

    /// <summary>
    /// Arbitra divergências pendentes contra a consulta oficial de CPF (Receita via Hub do
    /// Desenvolvedor, CADSUS como fallback). Roda sozinho ao fim de cada run; este método é o
    /// disparo manual pela tela.
    /// </summary>
    Task<ResultadoVerificacaoDivergencias> VerificarDivergenciasAsync(
        Guid? fonteId = null, int? max = null, CancellationToken ct = default);

    /// <summary>Marca uma divergência como falso positivo — descongela o campo para a origem.</summary>
    Task<DivergenciaIdentidadeDto> IgnorarDivergenciaAsync(
        Guid id, string? motivo = null, CancellationToken ct = default);

    /// <summary>Executa um job (chamado pelo runner em background). Não lança — registra o erro na execução.</summary>
    Task ExecutarAsync(PepImportacaoJob job, CancellationToken ct = default);
}

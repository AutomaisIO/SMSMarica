using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.Pep.Dtos;

/// <summary>Uma base de PEP candidata à importação (projeção de <c>IaFonte</c>).</summary>
public sealed record BasePepDto(
    Guid Id,
    string Nome,
    string Tipo,
    string Ambiente,
    bool Suportada,
    DateTime? UltimaSincronizacaoEm,
    /// <summary>Cursor de retomada salvo (cd_paciente do último bloco), se houver uma importação completa interrompida.</summary>
    long? CursorPacienteCd);

/// <summary>
/// Disparo de uma importação. <c>MaxMedicos/MaxPacientes</c> só valem no escopo Limitado.
/// <c>CursorPacienteInicial</c> só vale em Completo + Tudo (retomada / ponteiro manual).
/// </summary>
public sealed record IniciarImportacaoRequest(
    Guid FonteId,
    ModoSincronizacao Modo,
    EscopoSincronizacao Escopo,
    int? MaxMedicos,
    int? MaxPacientes,
    bool ApagarAntes,
    int? Concorrencia,
    long? CursorPacienteInicial = null);

/// <summary>Contadores por tipo de recurso de uma execução.</summary>
public sealed record ContadoresImportacaoDto(
    int Medicos,
    int Pacientes,
    int Encounters,
    int Conditions,
    int MedicationRequests,
    int DocumentReferences,
    int Observations,
    int Falhas);

/// <summary>
/// Status corrente da importação (run vivo se em execução; senão a última execução do banco).
/// </summary>
public sealed record StatusImportacaoDto(
    Guid? ExecucaoId,
    bool EmExecucao,
    Guid? FonteId,
    string? FonteNome,
    string? Modo,
    string? Escopo,
    string Status,
    string? FaseAtual,
    DateTime? IniciadoEm,
    DateTime? FinalizadoEm,
    double? DecorridoSegundos,
    ContadoresImportacaoDto Contadores,
    string? MensagemErro,
    IReadOnlyList<string> UltimasFalhas);

/// <summary>Uma falha durável de importação (projeção de <c>pep_sincronizacao_falha</c>).</summary>
public sealed record FalhaImportacaoDto(
    Guid Id,
    Guid ExecucaoId,
    Guid FonteId,
    string FonteSlug,
    long CdPaciente,
    string Mensagem,
    DateTime CriadoEm,
    DateTime? ResolvidoEm);

/// <summary>Item do histórico de execuções.</summary>
public sealed record ExecucaoImportacaoDto(
    Guid Id,
    Guid FonteId,
    string FonteNome,
    string Modo,
    string Escopo,
    string Status,
    DateTime IniciadoEm,
    DateTime? FinalizadoEm,
    double? DuracaoSegundos,
    ContadoresImportacaoDto Contadores,
    string? TemposJson,
    string? MensagemErro);

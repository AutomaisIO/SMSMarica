using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.Pep.Dtos;

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
    long? CursorPacienteInicial = null,
    /// <summary>
    /// Reimport DIRECIONADO: processa exatamente estes <c>cd_paciente</c> da origem,
    /// ignorando limite e recência. É o caminho para corrigir casos pontuais sem esperar
    /// o paciente ter atendimento novo — usado pelo reprocesso de divergências resolvidas.
    /// </summary>
    IReadOnlyList<long>? CdsPacientes = null,
    /// <summary>Reimport direcionado em bases de código NÃO numérico (Klinikos).</summary>
    IReadOnlyList<string>? CodigosPacientes = null);

/// <summary>Contadores por tipo de recurso de uma execução.</summary>
public sealed record ContadoresImportacaoDto(
    int Medicos,
    int Pacientes,
    int Encounters,
    int Conditions,
    int MedicationRequests,
    int DocumentReferences,
    int Observations,
    int Falhas,
    /// <summary>Reenvios por saturação transitória (farol de backpressure). Só no run vivo.</summary>
    int Retentativas = 0,
    /// <summary>
    /// Quantos dos <see cref="Pacientes"/> já estavam idênticos no hub e não viraram escrita.
    /// Num incremental o normal é ficar perto do total: o ciclo relê de propósito quem está
    /// internado. Quem mudou de verdade é <c>Pacientes - PacientesInalterados</c>.
    /// </summary>
    int PacientesInalterados = 0,
    /// <summary>O mesmo para <see cref="Medicos"/>, que é re-scan integral do cadastro.</summary>
    int MedicosInalterados = 0);

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
    string? MensagemErro,
    /// <summary>Origem do disparo: Manual (operador) ou Agendado (scheduler contínuo).</summary>
    string Disparo = "Manual");

/// <summary>Agenda do sincronismo contínuo de uma base (ADR-0024).</summary>
public sealed record AgendaPepDto(
    Guid FonteId,
    string FonteNome,
    bool Ativo,
    int IntervaloMinutos,
    TimeOnly? JanelaInicioLocal,
    TimeOnly? JanelaFimLocal,
    int MedicoRescanHoras,
    int FalhasConsecutivas,
    DateTime? ProximoRunEm,
    DateTime? PausadoAte,
    DateTime AtualizadoEm);

/// <summary>
/// Diagnóstico origem×hub (ADR-0024): marcas d'água, quanto ainda falta na ORIGEM desde cada
/// marca, e as contagens do hub por tipo (JSON cru do <c>/fhir/_estatisticas</c>). Pendências
/// próximas de zero logo após um ciclo = sincronismo em dia.
/// </summary>
public sealed record DiagnosticoPepDto(
    Guid FonteId,
    string FonteNome,
    string Slug,
    DateTime? UltimoSyncProfissionalEm,
    DateTime? UltimoSyncPacienteEm,
    DateTime? UltimoSyncAtendimentoEm,
    DateTime? UltimoSyncDocumentoEm,
    DateTime? UltimoSyncInternacaoEm,
    long? UltimoSyncLogDocumentoId,
    long? PacientesPendentes,
    long? BaasPendentes,
    long? FiasPendentes,
    long? EdocLogPendentes,
    System.Text.Json.JsonElement Hub);

/// <summary>Criação/edição da agenda de uma base. Janela em hora LOCAL de Brasília.</summary>
public sealed record SalvarAgendaPepRequest(
    Guid FonteId,
    bool Ativo,
    int IntervaloMinutos,
    TimeOnly? JanelaInicioLocal = null,
    TimeOnly? JanelaFimLocal = null,
    int? MedicoRescanHoras = null,
    DateTime? PausadoAte = null);

/// <summary>
/// Uma divergência de identidade origem×hub para o mesmo CPF (hoje: data de nascimento).
/// <c>Veredicto</c> diz quem está certo segundo a consulta oficial de CPF; enquanto não há
/// veredicto — ou quando ele aponta contra a origem — o campo fica congelado no hub.
/// </summary>
public sealed record DivergenciaIdentidadeDto(
    Guid Id,
    Guid FonteId,
    string FonteSlug,
    long CdPaciente,
    string Cpf,
    string Tipo,
    string ValorOrigem,
    string ValorHub,
    string? NomeOrigem,
    string? NomeHub,
    string? PatientIdHub,
    string Status,
    string Veredicto,
    string? VeredictoMotor,
    string? ValorCorreto,
    string? NomeOficial,
    string? Detalhe,
    int Ocorrencias,
    DateTime CriadoEm,
    DateTime AtualizadoEm,
    DateTime? VerificadoEm,
    DateTime? ResolvidoEm);

/// <summary>Contadores do relatório de divergências (cabeçalho da tela).</summary>
public sealed record ResumoDivergenciasDto(
    int Total,
    int Pendentes,
    int NaoConclusivas,
    int Ignoradas,
    int OrigemCorreta,
    int HubCorreto,
    int AmbosNegados,
    /// <summary>Quantos CPFs estão com o campo congelado agora (a origem não sobrescreve).</summary>
    int Congelados);

/// <summary>Pedido de arbitragem manual pela tela.</summary>
public sealed record VerificarDivergenciasRequest(Guid? FonteId = null, int? Max = null);

/// <summary>Marcar uma divergência como falso positivo.</summary>
public sealed record IgnorarDivergenciaRequest(string? Motivo = null);

/// <summary>Pausa administrativa do motor. <c>Horas</c> ausente ou 0 = retomar.</summary>
public sealed record PausarMotorRequest(Guid FonteId, int? Horas = null);

/// <summary>
/// Reprocessar da origem os pacientes das divergências já arbitradas como "origem correta".
/// <c>Ids</c> vazio = todas as elegíveis da base.
/// </summary>
public sealed record ReprocessarDivergenciasRequest(Guid FonteId, IReadOnlyList<Guid>? Ids = null);

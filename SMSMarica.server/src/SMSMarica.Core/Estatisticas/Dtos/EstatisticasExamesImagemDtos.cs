namespace SMSMarica.Core.Estatisticas.Dtos;

/// <summary>
/// Retrato agregado dos EXAMES DE IMAGEM num período. Visão gerencial, só leitura; todos os
/// números são contagens/médias (sem PII). O recorte é o satélite <c>ExameImagem</c> de
/// solicitações de categoria <c>Imagem</c> (ADR-0021), com escopo por unidade do usuário.
///
/// Âncora do período (<c>dataRef</c>): <c>RealizadoEm ?? DataAgendada ?? CriadoEm</c> — coloca o
/// exame realizado no dia em que aconteceu, o pendente no dia agendado e o resto no dia em que
/// entrou. Sempre UTC (nunca <c>DataEstudo</c>, que é wall-clock local e misturaria fuso).
/// </summary>
public sealed record EstatisticasExamesImagemDto(
    DateOnly De,
    DateOnly Ate,
    Guid? UnidadeId,
    ExamesImagemResumoDto Resumo,
    IReadOnlyList<SerieExamesDiaDto> PorDia,
    IReadOnlyList<RotuloContagemDto> PorModalidade,
    IReadOnlyList<RotuloContagemDto> PorUnidade,
    IReadOnlyList<RotuloContagemDto> PorStatus,
    IReadOnlyList<RotuloContagemDto> PorTipoExame,
    IReadOnlyList<RotuloContagemDto> PorMedico);

/// <summary>Cartões-resumo (KPIs) do período.</summary>
public sealed record ExamesImagemResumoDto(
    long TotalExames,
    long Realizados,
    long Laudados,
    long AguardandoLaudo,
    long LaudosEmitidos,
    long Cancelados,
    int MedicosLaudando,
    int DiasNoPeriodo,
    double MediaExamesDia,
    double PercentualLaudados,
    // Tempos médios (horas) por trecho do ciclo. Null quando não há amostra no período.
    double? TempoMedioChegadaExecucaoHoras,
    double? TempoMedioExecucaoLaudoHoras,
    double? TempoMedioTotalHoras,
    int AmostraChegadaExecucao,
    int AmostraExecucaoLaudo,
    int AmostraTotal);

/// <summary>Ponto da série temporal (um dia): exames registrados x realizados x laudados.</summary>
public sealed record SerieExamesDiaDto(DateOnly Dia, long Registrados, long Realizados, long Laudados);

/// <summary>
/// Linha da lista ANALÍTICA de exames de imagem que sustenta os agregados (uma por exame).
/// Contém PII de paciente — só é produzida para exportação autenticada e auditada, atrás do
/// gate <c>SolicitacoesExame</c>.
/// </summary>
public sealed record ExameImagemAnaliticoDto(
    string? NumeroSolicitacao,
    string AccessionNumber,
    string StudyInstanceUID,
    string? PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,
    DateOnly? PacienteNascimento,
    string Modalidade,
    string? TipoExame,
    string? UnidadeExecutante,
    string? UnidadeSolicitante,
    string Status,
    DateOnly? DataSolicitacao,
    DateTime? AutorizadoEm,
    DateTime? DataEstudo,
    DateTime? RealizadoEm,
    DateTime? LaudoFinalizadoEm,
    string? MedicoLaudo,
    string? MedicoCrm,
    double? TempoChegadaExecucaoHoras,
    double? TempoExecucaoLaudoHoras,
    double? TempoTotalHoras);

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

/// <summary>O que exportar na lista analítica de imagem.</summary>
public enum ConteudoExportacaoImagem
{
    Exames = 1,
    Laudos = 2,
    ExamesLaudos = 3,
}

/// <summary>
/// Pacote da exportação analítica: as duas visões materializadas (exames e laudos) do mesmo
/// recorte. O endpoint escolhe qual serializar conforme <see cref="ConteudoExportacaoImagem"/>.
/// SEM PII de paciente — apenas números do exame/solicitação/laudo (decisão do ticket #94).
/// </summary>
public sealed record ExportacaoImagemDto(
    IReadOnlyList<ExameImagemAnaliticoDto> Exames,
    IReadOnlyList<LaudoAnaliticoDto> Laudos);

/// <summary>Linha analítica de um EXAME (uma por exame). Sem identificação de paciente.</summary>
public sealed record ExameImagemAnaliticoDto(
    string? NumeroSolicitacao,
    string AccessionNumber,
    string StudyInstanceUID,
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

/// <summary>
/// Linha da exportação de FATURAMENTO de exames de imagem (uma por exame realizado). Diferente das
/// demais visões analíticas, esta CARREGA PII do paciente (nome, CPF, CNS, nascimento, CEP, celular)
/// porque o faturamento precisa localizar/identificar o cidadão — ticket #74. Não traz accession nem
/// números internos de controle. Ordenada por data de realização crescente na origem.
/// </summary>
public sealed record ExameFaturamentoDto(
    string Paciente,
    string? Cpf,
    string? Cns,
    DateOnly? Nascimento,
    string? Cep,
    string? Celular,
    string? Exame,
    DateTime? Realizacao);

/// <summary>Linha analítica de um LAUDO (uma por laudo finalizado). Sem identificação de paciente.</summary>
public sealed record LaudoAnaliticoDto(
    string? NumeroSolicitacao,
    string AccessionNumber,
    string StudyInstanceUID,
    int Versao,
    string Modalidade,
    string? TipoExame,
    string? UnidadeExecutante,
    DateTime? DataEstudo,
    DateTime? RealizadoEm,
    DateTime? LaudoFinalizadoEm,
    string? MedicoLaudo,
    string? MedicoCrm,
    double? TempoExecucaoLaudoHoras);

namespace SMSMais.Core.Integracoes.SisregWeb.Estatisticas.Dtos;

// ------------------------------------------------------------------ configuração

/// <summary>Um login que já apareceu como "Op. autorizador" no export da agenda.</summary>
/// <param name="Autorizacoes">Agendamentos autorizados por este login em todo o histórico importado.</param>
/// <param name="UsuarioNome">Nome do usuário do SMSMais que tem este login associado, se houver.</param>
public sealed record OperadorSisregDto(
    string Login,
    int Autorizacoes,
    DateOnly? PrimeiraAutorizacao,
    DateOnly? UltimaAutorizacao,
    bool Habilitado,
    Guid? UsuarioId,
    string? UsuarioNome);

public sealed record OperadoresConfiguracaoDto(IReadOnlyList<OperadorSisregDto> Operadores, int Habilitados);

public sealed record SalvarOperadoresHabilitadosRequest(IReadOnlyList<string>? Logins);

// ------------------------------------------------------------------ blocos comuns

/// <summary>Os números de um período, para os cartões e para comparar com o período anterior.</summary>
/// <param name="Operadores">Pessoas (ou logins sem pessoa associada) com pelo menos uma autorização.</param>
/// <param name="DiasComAtividade">Dias com pelo menos uma autorização de alguém da equipe.</param>
/// <param name="MediaDiaCorrido">Autorizações ÷ dias do período.</param>
/// <param name="MediaDiaComAtividade">Autorizações ÷ dias com atividade.</param>
/// <param name="EsperaMedianaDias">Mediana de dias entre o pedido e a autorização. Nulo no período anterior.</param>
/// <param name="AntecedenciaMedianaDias">Mediana de dias entre a autorização e o atendimento. Nulo no período anterior.</param>
public sealed record ResumoPeriodoDto(
    DateOnly De,
    DateOnly Ate,
    int Autorizacoes,
    decimal ValorRegulado,
    int Operadores,
    int DiasComAtividade,
    double MediaDiaCorrido,
    double MediaDiaComAtividade,
    double? EsperaMedianaDias,
    double? AntecedenciaMedianaDias,
    double PercentualFimDeSemana);

public sealed record SerieDiaDto(DateOnly Dia, int Autorizacoes, int Operadores, decimal ValorRegulado);

public sealed record SerieMesDto(DateOnly Mes, int Autorizacoes, int Operadores, decimal ValorRegulado);

/// <param name="IsoDia">1 = segunda … 7 = domingo.</param>
/// <param name="MediaPorDiaComAtividade">Autorizações ÷ quantos desses dias tiveram atividade.</param>
public sealed record DiaSemanaDto(
    int IsoDia, string Rotulo, int Autorizacoes, int DiasComAtividade, double MediaPorDiaComAtividade);

/// <param name="Operadores">Quantas pessoas autorizaram este item no período.</param>
public sealed record TopItemDto(string Rotulo, int Autorizacoes, decimal ValorRegulado, int Operadores);

/// <summary>Uma pessoa (ou um login sem pessoa associada) no período.</summary>
/// <param name="Chave">Identificador estável: <c>u:&lt;id do usuário&gt;</c> quando o login está associado a
/// um usuário (soma os logins habilitados dele) ou <c>l:&lt;LOGIN&gt;</c>.</param>
/// <param name="DiasTrabalhados">Dias do período com pelo menos uma autorização.</param>
/// <param name="DiasCorridos">Dias do período (fim − início + 1), trabalhando ou não.</param>
/// <param name="MediaDiaCorrido">Autorizações ÷ dias corridos — cai quando a pessoa não trabalha todo dia.</param>
/// <param name="MediaDiaTrabalhado">Autorizações ÷ dias trabalhados — a produção de um dia de trabalho.</param>
/// <param name="EsperaMedianaDias">Mediana de dias entre o pedido e a autorização.</param>
/// <param name="EsperaP90Dias">90% das autorizações foram de pedidos com até esta espera.</param>
/// <param name="AntecedenciaMedianaDias">Mediana de dias entre a autorização e o dia do atendimento.</param>
/// <param name="AgendaNovaMedianaDias">Mediana de dias entre a ativação da escala no SISREG e a
/// autorização que caiu nela — só escalas ativadas há até 90 dias.</param>
/// <param name="AgendaNovaAutorizacoes">Quantas autorizações entraram na conta acima.</param>
/// <param name="PercentualProprioPedido">Autorizações em que o mesmo login fez o pedido — alto indica
/// login de unidade marcando a própria agenda, não regulação.</param>
public sealed record OperadorPeriodoDto(
    string Chave,
    string Nome,
    Guid? UsuarioId,
    IReadOnlyList<string> Logins,
    int Autorizacoes,
    decimal ValorRegulado,
    int DiasTrabalhados,
    int DiasCorridos,
    double MediaDiaCorrido,
    double MediaDiaTrabalhado,
    int PicoDiario,
    DateOnly? DiaDoPico,
    double PercentualFimDeSemana,
    double? EsperaMedianaDias,
    double? EsperaP90Dias,
    double? AntecedenciaMedianaDias,
    double? AgendaNovaMedianaDias,
    int AgendaNovaAutorizacoes,
    int UnidadesExecutantes,
    int UnidadesSolicitantes,
    int Procedimentos,
    double PercentualProprioPedido);

// ------------------------------------------------------------------ telas

/// <param name="Habilitados">Logins marcados na configuração. Zero = a tela manda configurar.</param>
/// <param name="AutorizacoesTodosOsLogins">Autorizações do período de TODOS os logins, habilitados ou
/// não — para ver quanto da regulação a equipe configurada representa.</param>
/// <param name="MediaOperadoresPorDiaUtil">Pessoas ativas por dia útil (seg–sex com atividade).</param>
/// <param name="ConcentracaoTop3Percentual">Quanto das autorizações vem das 3 pessoas que mais autorizam.</param>
/// <param name="Pico">O dia de maior volume da equipe.</param>
public sealed record EquipeEstatisticaDto(
    int DiasCorridos,
    int Habilitados,
    ResumoPeriodoDto Atual,
    ResumoPeriodoDto Anterior,
    int AutorizacoesTodosOsLogins,
    double MediaOperadoresPorDiaUtil,
    double ConcentracaoTop3Percentual,
    SerieDiaDto? Pico,
    IReadOnlyList<SerieDiaDto> PorDia,
    IReadOnlyList<SerieMesDto> PorMes,
    IReadOnlyList<DiaSemanaDto> PorDiaSemana,
    IReadOnlyList<OperadorPeriodoDto> Ranking,
    IReadOnlyList<TopItemDto> TopProcedimentos,
    IReadOnlyList<TopItemDto> TopUnidadesExecutantes,
    IReadOnlyList<TopItemDto> TopUnidadesSolicitantes);

/// <param name="Metricas">Nulo quando a pessoa não autorizou nada no período.</param>
public sealed record IndividualEstatisticaDto(
    string Chave,
    string Nome,
    Guid? UsuarioId,
    IReadOnlyList<string> Logins,
    int DiasCorridos,
    OperadorPeriodoDto? Metricas,
    ResumoPeriodoDto Atual,
    ResumoPeriodoDto Anterior,
    IReadOnlyList<SerieDiaDto> PorDia,
    IReadOnlyList<SerieMesDto> PorMes,
    IReadOnlyList<DiaSemanaDto> PorDiaSemana,
    IReadOnlyList<TopItemDto> TopProcedimentos,
    IReadOnlyList<TopItemDto> TopUnidadesExecutantes,
    IReadOnlyList<TopItemDto> TopUnidadesSolicitantes);

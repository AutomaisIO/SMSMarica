namespace SMSMais.Core.Regulacao.Estatisticas.Dtos;

// ------------------------------------------------------------------ configuração

/// <summary>Um nome que já apareceu como "Usuário" na trilha de eventos do SER/SERNIT.</summary>
/// <param name="Nome">Como o sistema externo grava (normalizado em maiúsculas).</param>
/// <param name="Acoes">Eventos assinados por este nome em todo o histórico capturado.</param>
/// <param name="LotacaoMaisComum">A lotação que mais aparece nos eventos dele ("Gestor: …",
/// "Operador da Central: …") — ajuda a separar regulador de unidade.</param>
public sealed record OperadorExternoDto(
    string Nome,
    int Acoes,
    DateOnly? PrimeiraAcao,
    DateOnly? UltimaAcao,
    string? LotacaoMaisComum,
    bool Habilitado);

public sealed record OperadoresExternosConfiguracaoDto(IReadOnlyList<OperadorExternoDto> Operadores, int Habilitados);

public sealed record SalvarOperadoresExternosHabilitadosRequest(IReadOnlyList<string>? Nomes);

// ------------------------------------------------------------------ blocos comuns

/// <summary>Os números de um período, para os cartões e para comparar com o período anterior.</summary>
/// <param name="Acoes">Todos os eventos assinados pela equipe (qualquer verbo).</param>
/// <param name="Agendamentos">Agendar + Reagendar.</param>
/// <param name="Solicitacoes">Solicitações distintas em que a equipe mexeu.</param>
/// <param name="Operadores">Nomes com pelo menos um evento.</param>
/// <param name="DiasComAtividade">Dias com pelo menos um evento de alguém da equipe.</param>
/// <param name="EsperaMedianaDias">Mediana de dias entre a data da solicitação e o agendamento.
/// Nulo no período anterior.</param>
/// <param name="PercentualFimDeSemana">Parte dos eventos no sábado ou domingo.</param>
/// <param name="PercentualForaDoExpediente">Parte dos eventos antes das 7h ou a partir das 19h.</param>
public sealed record ResumoPeriodoExternoDto(
    DateOnly De,
    DateOnly Ate,
    int Acoes,
    int Agendamentos,
    int Cancelamentos,
    int FollowUps,
    int Pendencias,
    int Solicitacoes,
    int Operadores,
    int DiasComAtividade,
    double MediaDiaCorrido,
    double MediaDiaComAtividade,
    double? EsperaMedianaDias,
    double PercentualFimDeSemana,
    double PercentualForaDoExpediente);

public sealed record SerieDiaExternoDto(DateOnly Dia, int Acoes, int Operadores, int Agendamentos);

public sealed record SerieMesExternoDto(DateOnly Mes, int Acoes, int Operadores, int Agendamentos);

/// <param name="IsoDia">1 = segunda … 7 = domingo.</param>
public sealed record DiaSemanaExternoDto(
    int IsoDia, string Rotulo, int Acoes, int DiasComAtividade, double MediaPorDiaComAtividade);

/// <param name="Hora">0..23, no relógio de Brasília.</param>
public sealed record HoraExternoDto(int Hora, int Acoes, int Agendamentos);

/// <param name="Operadores">Quantas pessoas assinaram este item no período.</param>
public sealed record TopItemExternoDto(string Rotulo, int Acoes, int Operadores);

/// <summary>Uma pessoa (nome como o sistema externo grava) no período.</summary>
/// <param name="Chave">Identificador estável: <c>o:&lt;NOME&gt;</c>.</param>
/// <param name="DiasTrabalhados">Dias do período com pelo menos um evento.</param>
/// <param name="DiasCorridos">Dias do período (fim − início + 1), trabalhando ou não.</param>
/// <param name="EsperaMedianaDias">Mediana de dias entre a solicitação e o agendamento feito por ela.</param>
/// <param name="EsperaP90Dias">90% dos agendamentos dela foram de pedidos com até esta espera.</param>
/// <param name="HoraInicioMediana">Mediana da hora do primeiro evento de cada dia trabalhado (ex.: 8,5 = 8h30).</param>
/// <param name="HoraFimMediana">Mediana da hora do último evento de cada dia trabalhado.</param>
/// <param name="Recursos">Recursos (procedimentos em texto) diferentes tocados.</param>
/// <param name="UnidadesExecutoras">Unidades executoras diferentes nos eventos dela.</param>
/// <param name="Lotacoes">Lotações com que assinou no período.</param>
public sealed record OperadorExternoPeriodoDto(
    string Chave,
    string Nome,
    int Acoes,
    int Agendamentos,
    int Cancelamentos,
    int FollowUps,
    int Pendencias,
    int Solicitacoes,
    int DiasTrabalhados,
    int DiasCorridos,
    double MediaDiaCorrido,
    double MediaDiaTrabalhado,
    int PicoDiario,
    DateOnly? DiaDoPico,
    double PercentualFimDeSemana,
    double? EsperaMedianaDias,
    double? EsperaP90Dias,
    double? HoraInicioMediana,
    double? HoraFimMediana,
    int Recursos,
    int UnidadesExecutoras,
    IReadOnlyList<string> Lotacoes);

// ------------------------------------------------------------------ telas

/// <param name="Habilitados">Nomes marcados na configuração. Zero = a tela manda configurar.</param>
/// <param name="AcoesTodosOsOperadores">Eventos do período de TODOS os nomes, habilitados ou não —
/// para ver quanto da trilha a equipe configurada representa.</param>
/// <param name="MediaOperadoresPorDiaUtil">Pessoas ativas por dia útil (seg–sex com atividade).</param>
/// <param name="ConcentracaoTop3Percentual">Quanto dos eventos vem das 3 pessoas mais ativas.</param>
public sealed record EquipeExternaEstatisticaDto(
    string Fonte,
    int DiasCorridos,
    int Habilitados,
    ResumoPeriodoExternoDto Atual,
    ResumoPeriodoExternoDto Anterior,
    int AcoesTodosOsOperadores,
    double MediaOperadoresPorDiaUtil,
    double ConcentracaoTop3Percentual,
    SerieDiaExternoDto? Pico,
    IReadOnlyList<SerieDiaExternoDto> PorDia,
    IReadOnlyList<SerieMesExternoDto> PorMes,
    IReadOnlyList<DiaSemanaExternoDto> PorDiaSemana,
    IReadOnlyList<HoraExternoDto> PorHora,
    IReadOnlyList<OperadorExternoPeriodoDto> Ranking,
    IReadOnlyList<TopItemExternoDto> PorTipoEvento,
    IReadOnlyList<TopItemExternoDto> TopRecursos,
    IReadOnlyList<TopItemExternoDto> TopUnidadesExecutoras,
    IReadOnlyList<TopItemExternoDto> TopLotacoes);

/// <param name="Metricas">Nulo quando a pessoa não assinou nada no período.</param>
public sealed record IndividualExternoEstatisticaDto(
    string Fonte,
    string Chave,
    string Nome,
    int DiasCorridos,
    OperadorExternoPeriodoDto? Metricas,
    ResumoPeriodoExternoDto Atual,
    ResumoPeriodoExternoDto Anterior,
    IReadOnlyList<SerieDiaExternoDto> PorDia,
    IReadOnlyList<SerieMesExternoDto> PorMes,
    IReadOnlyList<DiaSemanaExternoDto> PorDiaSemana,
    IReadOnlyList<HoraExternoDto> PorHora,
    IReadOnlyList<TopItemExternoDto> PorTipoEvento,
    IReadOnlyList<TopItemExternoDto> TopRecursos,
    IReadOnlyList<TopItemExternoDto> TopUnidadesExecutoras,
    IReadOnlyList<TopItemExternoDto> TopLotacoes);

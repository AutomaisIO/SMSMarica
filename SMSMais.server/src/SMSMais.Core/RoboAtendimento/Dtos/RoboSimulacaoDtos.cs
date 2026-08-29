namespace SMSMais.Core.RoboAtendimento.Dtos;

/// <summary>Um turno de ensaio do robô: a mensagem do cidadão e, opcionalmente, o histórico e o
/// assunto forçado (vazio = deixa o classificador decidir, como no atendimento real).</summary>
public sealed record SimularRoboRequest(
    string Mensagem,
    Guid? AssuntoId = null,
    IReadOnlyList<SimularRoboTurnoDto>? Historico = null);

/// <summary>Turno do histórico simulado. <c>Papel</c>: cidadao | atendente | robo | sistema.</summary>
public sealed record SimularRoboTurnoDto(string Papel, string Texto);

/// <summary>Comando que o robô chamaria, com o que ele mandou e o que recebeu de volta.</summary>
/// <param name="Simulado"><c>true</c> = comando de escrita, NÃO executado (resultado fingido).</param>
public sealed record RoboSimulacaoChamadaDto(
    string Comando,
    string? EntradaJson,
    string Resultado,
    bool Sucesso,
    bool Simulado);

/// <summary>Resultado do ensaio — nada foi enviado ao cidadão.</summary>
public sealed record RoboSimulacaoDto(
    string? Assunto,
    string Modelo,
    string Texto,
    bool HandOff,
    string? MotivoHandOff,
    double? Confianca,
    IReadOnlyList<RoboSimulacaoChamadaDto> Chamadas,
    long? TokensEntrada,
    long? TokensSaida,
    decimal? CustoUsd,
    long DuracaoMs,
    bool DentroDoHorario);

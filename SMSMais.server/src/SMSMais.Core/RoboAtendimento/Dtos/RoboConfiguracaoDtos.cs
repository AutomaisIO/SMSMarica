using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Dtos;

/// <summary>Configuração global do robô (singleton).</summary>
public sealed record RoboConfiguracaoDto(
    bool Ativo,
    string PersonaGlobal,
    string ModeloPadrao,
    string NomeExibicao,
    string? MensagemHandOff,
    string? MensagemForaHorario,
    TimeOnly? HoraAtendimentoHumanoInicio,
    TimeOnly? HoraAtendimentoHumanoFim,
    int? DiasSemanaAtendimentoHumano,
    MotorRobo Motor);

public sealed record SalvarRoboConfiguracaoRequest(
    bool Ativo,
    string PersonaGlobal,
    string ModeloPadrao,
    string NomeExibicao,
    string? MensagemHandOff,
    string? MensagemForaHorario,
    TimeOnly? HoraAtendimentoHumanoInicio,
    TimeOnly? HoraAtendimentoHumanoFim,
    int? DiasSemanaAtendimentoHumano = 62,
    MotorRobo Motor = MotorRobo.Assinatura);

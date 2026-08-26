namespace SMSMais.Core.RoboAtendimento.Dtos;

/// <summary>Configuração global do robô (singleton).</summary>
public sealed record RoboConfiguracaoDto(
    bool Ativo,
    string PersonaGlobal,
    string ModeloPadrao,
    string NomeExibicao,
    string? MensagemHandOff,
    string? MensagemForaHorario);

public sealed record SalvarRoboConfiguracaoRequest(
    bool Ativo,
    string PersonaGlobal,
    string ModeloPadrao,
    string NomeExibicao,
    string? MensagemHandOff,
    string? MensagemForaHorario);

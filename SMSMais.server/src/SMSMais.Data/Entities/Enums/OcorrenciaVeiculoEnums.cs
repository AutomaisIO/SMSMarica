namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// O que o motorista reportou pelo botão REPORT do tablet do veículo. Persistido como inteiro:
/// não renumerar nem reaproveitar valores.
/// </summary>
public enum TipoOcorrenciaVeiculo
{
    Acidente = 1,
    PaneVeiculo = 2,
    PacienteMalEstar = 3,

    /// <summary>Exige descrição (texto livre do motorista).</summary>
    Outro = 4,
}

/// <summary>
/// Andamento da ocorrência na central: Aberta → EmAtendimento → Encerrada (ou direto de Aberta para
/// Encerrada). Persistido como inteiro: não renumerar.
/// </summary>
public enum SituacaoOcorrenciaVeiculo
{
    Aberta = 1,
    EmAtendimento = 2,
    Encerrada = 3,
}

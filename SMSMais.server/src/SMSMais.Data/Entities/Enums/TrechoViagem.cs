namespace SMSMais.Data.Entities.Enums;

/// <summary>A parte da viagem: a ida (casa → unidade) ou o resgate (unidade → casa). Cada uma pode ter
/// motorista e veículo diferentes — e a sua avaliação.</summary>
public enum TrechoViagem
{
    Ida = 1,
    Resgate = 2,
}

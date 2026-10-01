namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Regime de contratação de um profissional (hoje usado no <see cref="Motorista"/> do Transporte):
/// CLT (carteira assinada) ou RPA (autônomo pago por recibo). Valor inteiro estável.
/// </summary>
public enum RegimeContratacao
{
    Clt = 1,
    Rpa = 2,
}

namespace SMSMais.Data.Entities.Enums;

/// <summary>Resultado de um contato MANUAL com o paciente (registro da atendente).</summary>
public enum ResultadoContato
{
    Atendeu = 1,
    NaoAtendeu = 2,
    CaixaPostal = 3,
    NumeroInvalido = 4,
    Outro = 9,
}

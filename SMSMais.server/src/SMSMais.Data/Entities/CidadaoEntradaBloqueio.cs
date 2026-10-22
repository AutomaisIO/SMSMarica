namespace SMSMais.Data.Entities;

/// <summary>
/// Freio da entrada no app do cidadão por CPF (ADR-0078): CPF + data de nascimento errados repetidas
/// vezes bloqueiam a entrada daquele CPF por um tempo — é o que impede adivinhar a data de nascimento
/// de alguém. Fica no banco de propósito: um reinício do servidor não pode zerar a contagem.
/// </summary>
public sealed class CidadaoEntradaBloqueio
{
    /// <summary>Só os 11 dígitos.</summary>
    public string Cpf { get; set; } = string.Empty;

    /// <summary>Erros na janela que começou em <see cref="PrimeiraFalhaEm"/>.</summary>
    public int Falhas { get; set; }

    public DateTime PrimeiraFalhaEm { get; set; }

    /// <summary>Preenchido quando estourou o limite: até lá, a entrada desse CPF é recusada.</summary>
    public DateTime? BloqueadoAte { get; set; }
}

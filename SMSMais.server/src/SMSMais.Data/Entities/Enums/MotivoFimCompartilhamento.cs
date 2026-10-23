namespace SMSMais.Data.Entities.Enums;

/// <summary>Por que o paciente deixou de compartilhar a localização com o motorista (ADR-0082).</summary>
public enum MotivoFimCompartilhamento
{
    /// <summary>O paciente tocou em "Parar" no app.</summary>
    PeloPaciente = 1,

    /// <summary>Embarcou: dentro do carro vale a posição do veículo.</summary>
    Embarque = 2,

    /// <summary>O motorista marcou que ele não estava na coleta.</summary>
    Ausencia = 3,

    /// <summary>A espera pelo embarque acabou de outro jeito (cancelamento, outro dia, já em casa).</summary>
    ForaDaJanela = 4,
}

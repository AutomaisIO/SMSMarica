namespace SMSMais.Data.Entities.Enums;

/// <summary>Como o paciente se desloca no transporte. Valor inteiro estável (gravado no banco).</summary>
public enum MobilidadeTransporte
{
    /// <summary>Anda e senta no banco sem necessidade especial.</summary>
    Independente = 1,

    /// <summary>Cadeirante que passa para o banco: a cadeira dobra e vai guardada.</summary>
    CadeiranteTransfereParaBanco = 2,

    /// <summary>Cadeirante que viaja na própria cadeira: só em veículo adaptado.</summary>
    CadeiranteVeiculoAdaptado = 3,

    /// <summary>Viaja deitado (maca).</summary>
    Maca = 4,
}

namespace SMSMarica.Data.Entities.Enums;

public enum StatusSessao
{
    Pendente = 1,
    Confirmada = 2,
    Realizada = 3,
    Cancelada = 4,
    /// <summary>
    /// Chegou na data prevista mas o translado não aconteceu — registrar motivo.
    /// Diferente de <see cref="Cancelada"/>: cancelada é anterior ao dia.
    /// </summary>
    NaoRealizada = 5,
}

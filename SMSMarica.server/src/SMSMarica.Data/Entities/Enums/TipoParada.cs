namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Natureza de uma parada na rota gerada (TFD): coleta do paciente em Maricá,
/// entrega no destino, ou retorno (volta). Valor inteiro estável.
/// </summary>
public enum TipoParada
{
    Coleta = 1,
    Destino = 2,
    Retorno = 3,
}

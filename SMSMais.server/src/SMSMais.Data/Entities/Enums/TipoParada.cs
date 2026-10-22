namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Natureza de uma parada na rota gerada (TFD): coleta do paciente em Maricá,
/// entrega no destino, ou retorno (o resgate: buscar na unidade e deixar em casa). Valor inteiro estável.
/// </summary>
public enum TipoParada
{
    Coleta = 1,
    Destino = 2,
    Retorno = 3,
}

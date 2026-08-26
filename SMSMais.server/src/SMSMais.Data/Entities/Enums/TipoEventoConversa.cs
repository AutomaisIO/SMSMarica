namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Tipo de evento na trilha append-only de uma conversa (<see cref="Conversas.ConversaEvento"/>):
/// quem pegou, encaminhou ou transferiu, e quando. O valor inteiro é estável — não renumerar.
/// </summary>
public enum TipoEventoConversa
{
    Criada = 1,

    /// <summary>Atribuída a um operador (por regra/sistema).</summary>
    Atribuida = 2,

    /// <summary>Um operador "pegou" (takeover) a conversa para si.</summary>
    Assumida = 3,

    /// <summary>Encaminhada a outro operador (colega).</summary>
    Transferida = 4,

    /// <summary>Encaminhada para outra unidade.</summary>
    EncaminhadaUnidade = 5,

    Resolvida = 6,
    Reaberta = 7,
    Fechada = 8,

    /// <summary>Devolvida à fila (o responsável abriu mão; a conversa volta a ficar sem dono).</summary>
    Devolvida = 9,

    /// <summary>Devolvida AO ROBÔ ("Atendente Virtual"): volta à fila e re-arma o robô, que retoma
    /// de onde parou na janela corrente.</summary>
    EncaminhadaRobo = 10,
}

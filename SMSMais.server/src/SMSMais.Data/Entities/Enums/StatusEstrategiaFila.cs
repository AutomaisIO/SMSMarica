namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Estado de uma estratégia de fila (ADR-0058). É um ciclo de planejamento, não de execução:
/// <b>nada aqui escreve no SISREG nem em sistema nenhum</b> — a estratégia é guardada para ser
/// consultada, e "aplicada" é a anotação humana de que alguém a executou por fora.
/// </summary>
public enum StatusEstrategiaFila
{
    /// <summary>Em elaboração: parâmetros e rodadas ainda mudando.</summary>
    Rascunho = 1,

    /// <summary>O gestor deu por fechada — é a proposta a consultar.</summary>
    Pronta = 2,

    /// <summary>Alguém executou a proposta no mundo real (anotação, com data e nota).</summary>
    Aplicada = 3,

    /// <summary>Guardada por histórico; some das listas padrão.</summary>
    Arquivada = 4,
}

/// <summary>Como uma rodada da estratégia foi produzida.</summary>
public enum ModoRodadaEstrategia
{
    /// <summary>O operador mexeu nos parâmetros e mandou simular — sem IA.</summary>
    Manual = 1,

    /// <summary>O agente escolheu os parâmetros livres e propôs ações.</summary>
    Agente = 2,
}

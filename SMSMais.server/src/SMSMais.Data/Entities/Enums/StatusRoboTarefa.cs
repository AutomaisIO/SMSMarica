namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Estado de uma tarefa da fila do robô de atendimento (robo_tarefa). O worker drena as
/// Pendentes; HandOff é terminal e sinaliza que a conversa foi devolvida ao humano.
/// Valor inteiro estável (persistido) — não renumerar.
/// </summary>
public enum StatusRoboTarefa
{
    Pendente = 1,
    Processando = 2,
    Concluida = 3,
    Falha = 4,

    /// <summary>Terminal: o robô decidiu (ou foi obrigado a) devolver a conversa ao humano.</summary>
    HandOff = 5,
}

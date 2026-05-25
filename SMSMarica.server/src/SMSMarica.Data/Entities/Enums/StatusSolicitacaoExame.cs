namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Estados do ciclo de vida de uma solicitação de exame, do pedido ao laudo.
/// Transições válidas:
/// Solicitada → Agendada (UPS-RS aceitou o workitem)
/// Solicitada/Agendada → Cancelada (cancelamento manual)
/// Agendada → EmExecucao (study iniciou no PACS — parcial)
/// Agendada/EmExecucao → Realizada (study completo detectado no PACS)
/// Realizada → Laudada (Laudo finalizado pelo radiologista)
/// </summary>
public enum StatusSolicitacaoExame
{
    Solicitada = 1,
    Agendada = 2,
    EmExecucao = 3,
    Realizada = 4,
    Laudada = 5,
    Cancelada = 6,
}

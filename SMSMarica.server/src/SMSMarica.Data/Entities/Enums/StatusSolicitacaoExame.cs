namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Estados do ciclo de vida de uma solicitação de exame, do pedido ao laudo.
///
/// Transições válidas:
///   Solicitada → Enviada      (POST UPS-RS retornou 201)
///   Enviada    → Agendada     (GET no workitem confirmou existência no PACS)
///   Enviada    → Solicitada   (GET 404 — workitem sumiu do PACS, vai reenviar)
///   Solicitada/Enviada → Solicitada (falha temporária — fica retentando com backoff)
///   Solicitada/Enviada/Agendada → Cancelada (cancelamento manual)
///   Agendada   → EmExecucao   (study parcial chegou no PACS)
///   Agendada/EmExecucao → Realizada (study completo no PACS)
///   Realizada  → Laudada      (Laudo finalizado pelo radiologista)
/// </summary>
public enum StatusSolicitacaoExame
{
    Solicitada = 1,
    Agendada = 2,
    EmExecucao = 3,
    Realizada = 4,
    Laudada = 5,
    Cancelada = 6,

    /// <summary>POST UPS-RS deu 201 — aguardando GET de confirmação no próximo tick do worker.</summary>
    Enviada = 7,
}

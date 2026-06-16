namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Estados do ciclo de vida de uma solicitação de exame, do pedido ao laudo.
///
/// Transições válidas (fluxo Modality Worklist — MWL):
///   Solicitada → Enviada      (POST /mwlitems aceito pelo dcm4chee — nosso PACS recebeu)
///   Enviada    → Recebida     (GET confirma que o item está na worklist consultável pela máquina)
///   Recebida   → Agendada     (sistema reconfirma e consolida todos os passos antecessores)
///   Enviada/Recebida → Solicitada (item sumiu do PACS — vai reenviar)
///   Solicitada/Enviada/Recebida → Solicitada (falha temporária — retenta com backoff)
///   Solicitada/Enviada/Recebida/Agendada → Cancelada (cancelamento manual)
///   Agendada   → EmExecucao   (study parcial chegou no PACS)
///   Agendada/EmExecucao → Realizada (study completo no PACS)
///   Realizada  → Laudada      (Laudo finalizado pelo radiologista)
///
/// Nota: classic MWL (C-FIND) é stateless — o dcm4chee não expõe via REST o evento
/// exato "a máquina consultou". "Recebida" usa a confirmação de presença do item na
/// worklist consultável como prova de que o equipamento PODE listá-lo.
/// </summary>
public enum StatusSolicitacaoExame
{
    Solicitada = 1,
    Agendada = 2,
    EmExecucao = 3,
    Realizada = 4,
    Laudada = 5,
    Cancelada = 6,

    /// <summary>POST /mwlitems aceito — nosso PACS recebeu o item. Aguardando confirmação.</summary>
    Enviada = 7,

    /// <summary>dcm4chee confirma que o item está na worklist consultável pela máquina
    /// (entre Enviada e Agendada).</summary>
    Recebida = 8,
}

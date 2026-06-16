namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Estados do ciclo de vida de uma solicitação de exame, do pedido ao laudo.
///
/// Transições válidas (fluxo Modality Worklist — MWL):
///   Solicitada → Enviada      (POST /mwlitems aceito pelo dcm4chee — nosso PACS recebeu)
///   Enviada    → Recebida     (GET confirma o item na worklist — TERMINAL do envio)
///   Enviada    → Solicitada   (item sumiu do PACS — vai reenviar)
///   Solicitada/Enviada → Solicitada (falha temporária — retenta com backoff)
///   Solicitada/Enviada/Recebida → Cancelada (cancelamento manual)
///   Recebida   → EmExecucao   (study parcial chegou no PACS)
///   Recebida/EmExecucao → Realizada (study completo no PACS)
///   Realizada  → Laudada      (Laudo finalizado pelo radiologista)
///
/// Nota: classic MWL (C-FIND) é stateless — o dcm4chee não expõe via REST o evento
/// exato "a máquina consultou". "Recebida" usa a confirmação de presença do item na
/// worklist consultável como prova de que o equipamento PODE listá-lo.
/// </summary>
public enum StatusSolicitacaoExame
{
    Solicitada = 1,

    /// <summary>DESCONTINUADO 2026-06-16 — o envio agora termina em <see cref="Recebida"/>
    /// (sem gatilho confiável para "agendar"). Mantido só para linhas legadas (migradas).</summary>
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

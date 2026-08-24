namespace SMSMais.Data.Entities.Enums;

/// <summary>Etapa da conversa de cancelamento no WhatsApp (máquina de estados por telefone).</summary>
public enum EtapaConfirmacaoAgendamento
{
    /// <summary>Paciente tocou "Não poderei comparecer" — aguardando confirmar se cancela mesmo.</summary>
    AguardandoConfirmacaoCancelamento = 1,

    /// <summary>Cancelamento confirmado — aguardando o motivo em texto livre.</summary>
    AguardandoMotivo = 2,
}

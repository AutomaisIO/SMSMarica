namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Resposta do PACIENTE à notificação do agendamento. Independente do Status
/// operacional da solicitação: Cancelada aqui NÃO cancela o exame — sinaliza a
/// intenção do paciente para a equipe decidir (tela de gestão).
/// </summary>
public enum StatusConfirmacaoAgendamento
{
    Pendente = 1,
    Confirmada = 2,
    Cancelada = 3,
}

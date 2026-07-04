namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Ciclo de vida da notificação WhatsApp de um agendamento.
///
/// Transições:
///   Pendente → Enviada            (template aceito pela Meta)
///   Enviada  → Entregue → Lida    (recibos via webhook value.statuses)
///   Enviada/Entregue → Pendente   (falha de entrega retentável — reenvia com link novo)
///   Pendente → Falha              (esgotou tentativas ou erro Meta permanente)
///   Pendente → SemTelefoneValido  (nenhum celular BR válido — nem tenta)
/// </summary>
public enum StatusNotificacaoAgendamento
{
    Pendente = 1,
    Enviada = 2,
    Entregue = 3,
    Lida = 4,
    Falha = 5,
    SemTelefoneValido = 6,
}

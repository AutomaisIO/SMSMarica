namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Estados de um agendamento de consulta. Valor inteiro estável (persistido).
/// Transições registram timestamp próprio na entidade <c>Agendamento</c>.
/// </summary>
public enum StatusAgendamento
{
    /// <summary>Marcado, aguardando confirmação/comparecimento.</summary>
    Agendado = 1,

    /// <summary>Confirmado pelo paciente/unidade.</summary>
    Confirmado = 2,

    /// <summary>Consulta realizada.</summary>
    Realizado = 3,

    /// <summary>Cancelado (libera o horário).</summary>
    Cancelado = 4,

    /// <summary>Paciente faltou (não comparecimento registrado).</summary>
    Faltou = 5,
}

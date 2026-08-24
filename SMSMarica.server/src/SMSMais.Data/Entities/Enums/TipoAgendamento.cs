namespace SMSMais.Data.Entities.Enums;

/// <summary>Natureza do agendamento notificado ao paciente (WhatsApp/app).</summary>
public enum TipoAgendamento
{
    Exame = 1,

    /// <summary>Reservado — consultas ainda não são importadas/notificadas.</summary>
    Consulta = 2,
}

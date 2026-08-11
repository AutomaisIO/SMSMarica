namespace SMSMarica.Data.Entities.Enums;

/// <summary>Ciclo do alarme de identidade de exame. Ver <see cref="ExameIncidenteIdentidade"/>.</summary>
public enum StatusIncidenteIdentidade
{
    /// <summary>Em quarentena: bloqueia laudo, aviso ao paciente e conciliação do estudo.</summary>
    Aberto = 1,

    /// <summary>A correção foi feita — o estudo está no paciente certo.</summary>
    Resolvido = 2,

    /// <summary>Falso alarme: estava certo o tempo todo. Levanta a quarentena sem corrigir nada.</summary>
    Descartado = 3,
}

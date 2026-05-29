namespace SMSMarica.Data.Entities.Fhir.Enums;

/// <summary>
/// Tipo de deficiência (eSUS APS / DataSUS). Múltiplas podem coexistir
/// — modelado como tabela paralela patient_disability (não enum único).
/// </summary>
public enum DisabilityType
{
    Outro = 0,
    Auditiva = 1,
    Visual = 2,
    Intelectual = 3,
    Fisica = 4,
    Mental = 5,
    Multipla = 6,
}

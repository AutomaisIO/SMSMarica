namespace SMSMarica.Data.Entities.Fhir.Enums;

/// <summary>FHIR R4 HumanName.use.</summary>
public enum NameUse
{
    Usual = 0,
    Official = 1,
    Temp = 2,
    /// <summary>Nickname — apelido / nome afetivo no contexto BR.</summary>
    Nickname = 3,
    Anonymous = 4,
    Old = 5,
    /// <summary>Maiden — nome de solteira (mãe usa maiden frequentemente).</summary>
    Maiden = 6,
}

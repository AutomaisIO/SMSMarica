namespace SMSMarica.Data.Entities.Fhir.Enums;

/// <summary>
/// Escolaridade (Portaria GM/MS 1.434/2020 — eSUS APS). Códigos DataSUS.
/// </summary>
public enum EducationLevel
{
    NaoInformado = 0,
    SemEscolaridade = 1,
    FundamentalIncompleto = 2,
    FundamentalCompleto = 3,
    MedioIncompleto = 4,
    MedioCompleto = 5,
    SuperiorIncompleto = 6,
    SuperiorCompleto = 7,
    Especializacao = 8,
    Mestrado = 9,
    Doutorado = 10,
}

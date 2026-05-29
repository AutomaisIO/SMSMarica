namespace SMSMarica.Data.Entities.Fhir.Enums;

/// <summary>
/// FHIR identifier-type-codes (subset relevante BR). Combinado com Identifier.system,
/// define semântica do valor.
/// </summary>
public enum IdentifierTypeCode
{
    /// <summary>Outro / não codificado.</summary>
    Other = 0,
    /// <summary>MR — Medical Record Number (prontuário).</summary>
    MedicalRecord = 1,
    /// <summary>NI — National identifier (CNS, RG, RNE).</summary>
    NationalIdentifier = 2,
    /// <summary>PPN — Passport.</summary>
    Passport = 3,
    /// <summary>DL — Driver License.</summary>
    DriverLicense = 4,
    /// <summary>SS — Social Security (BR usa pra CPF).</summary>
    SocialSecurity = 5,
    /// <summary>TAX — Tax (PIS/PASEP, CNPJ).</summary>
    TaxId = 6,
    /// <summary>BC — Birth Certificate (certidão de nascimento).</summary>
    BirthCertificate = 7,
    /// <summary>BR-CNS especificamente.</summary>
    Cns = 8,
    /// <summary>BR-CPF especificamente.</summary>
    Cpf = 9,
    /// <summary>BR-RG especificamente (carteira de identidade).</summary>
    Rg = 10,
    /// <summary>Conselho profissional (CRM, COREN, CRO…).</summary>
    ProfessionalCouncil = 11,
}

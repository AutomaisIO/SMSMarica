namespace SMSMarica.Data.Entities.Fhir.Enums;

/// <summary>
/// Identidade de gênero (separada de Patient.gender que é sexo administrativo).
/// Codificação compatível com extensão FHIR genderIdentity + valueset BR.
/// </summary>
public enum GenderIdentity
{
    NaoInformado = 0,
    /// <summary>Identifica-se como homem cisgênero.</summary>
    HomemCis = 1,
    /// <summary>Identifica-se como mulher cisgênero.</summary>
    MulherCis = 2,
    /// <summary>Homem trans / transmasculino.</summary>
    HomemTrans = 3,
    /// <summary>Mulher trans / transfeminina.</summary>
    MulherTrans = 4,
    /// <summary>Não-binário.</summary>
    NaoBinario = 5,
    /// <summary>Outro.</summary>
    Outro = 6,
    PrefereNaoDizer = 7,
}

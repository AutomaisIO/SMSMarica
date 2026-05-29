namespace SMSMarica.Data.Entities.Fhir.Enums;

/// <summary>
/// Base legal LGPD (Lei 13.709/2018) para tratamento de dado pessoal sensível
/// de saúde (art. 11). Documentada por requisito regulatório.
/// </summary>
public enum LgpdLegalBasis
{
    NaoInformado = 0,
    /// <summary>Art. 11 II "b" — execução de política pública (SUS).</summary>
    PoliticaPublicaSus = 1,
    /// <summary>Art. 11 II "f" — tutela da saúde, em procedimento por profissional de saúde.</summary>
    TutelaSaude = 2,
    /// <summary>Art. 11 II "a" — consentimento explícito do titular.</summary>
    ConsentimentoExplicito = 3,
    /// <summary>Art. 11 II "c" — cumprimento de obrigação legal.</summary>
    ObrigacaoLegal = 4,
    /// <summary>Art. 11 II "d" — estudos por órgão de pesquisa.</summary>
    Pesquisa = 5,
    /// <summary>Art. 11 II "e" — exercício regular de direitos.</summary>
    ExercicioDireitos = 6,
    /// <summary>Art. 11 II "g" — proteção da vida.</summary>
    ProtecaoVida = 7,
}

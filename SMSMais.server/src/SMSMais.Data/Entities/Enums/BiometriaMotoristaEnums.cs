namespace SMSMais.Data.Entities.Enums;

/// <summary>Em que momento o tablet conferiu o motorista (ADR-0073).</summary>
public enum MomentoVerificacaoFacial
{
    /// <summary>Entrada no app do tablet.</summary>
    Login = 1,

    /// <summary>Conferência antes de abrir a navegação de um translado.</summary>
    InicioTranslado = 2,

    /// <summary>Cadastro do rosto no primeiro acesso (depois do código no WhatsApp).</summary>
    PrimeiroAcesso = 3,
}

/// <summary>Como o motorista foi conferido.</summary>
public enum MeioVerificacaoMotorista
{
    /// <summary>Rosto comparado no tablet com a foto de referência.</summary>
    Rosto = 1,

    /// <summary>Código de uso único gerado pela central no painel.</summary>
    CodigoCentral = 2,
}

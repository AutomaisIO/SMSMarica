namespace SMSMais.Data.Entities.Enums;

/// <summary>De onde veio o nome do acompanhante, conferido pelo par CPF + nascimento.
/// Valor inteiro estável (gravado no banco).</summary>
public enum FonteNomeAcompanhante
{
    /// <summary>Cadastro de paciente que já existia na base (CPF e nascimento batem).</summary>
    Base = 1,

    /// <summary>Consulta ao proxy de CPF (Receita ou CADSUS, conforme o motor que respondeu).</summary>
    ConsultaCpf = 2,
}

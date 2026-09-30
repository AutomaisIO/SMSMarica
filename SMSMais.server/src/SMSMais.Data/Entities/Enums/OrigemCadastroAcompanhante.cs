namespace SMSMais.Data.Entities.Enums;

/// <summary>Onde o acompanhante foi cadastrado. Valor inteiro estável (gravado no banco).</summary>
public enum OrigemCadastroAcompanhante
{
    /// <summary>Pela equipe, no painel.</summary>
    Painel = 1,

    /// <summary>Pelo próprio paciente, no app do cidadão.</summary>
    App = 2,
}

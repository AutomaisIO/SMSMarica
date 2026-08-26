namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Como uma condição de ativação de um assunto do robô casa com a mensagem do cidadão.
/// Usado no pré-match barato da classificação (antes de chamar a IA). Valor inteiro estável.
/// </summary>
public enum TipoCondicaoRobo
{
    /// <summary>Casa se o texto contém a palavra (acento/caixa-insensível).</summary>
    PalavraChave = 1,

    /// <summary>Casa se o texto bate com a expressão regular.</summary>
    Regex = 2,

    /// <summary>Casa se o texto contém a frase inteira (acento/caixa-insensível).</summary>
    Frase = 3,
}

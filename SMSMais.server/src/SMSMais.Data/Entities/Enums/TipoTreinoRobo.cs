namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Natureza de um "treino" de um assunto do robô — o material que o operador vai inserindo
/// para personalizar e melhorar o atendimento. Tudo é injetado no prompt da sessão do assunto.
/// Valor inteiro estável (persistido) — não renumerar.
/// </summary>
public enum TipoTreinoRobo
{
    /// <summary>Instrução/diretriz de como o robô deve agir neste assunto.</summary>
    Instrucao = 1,

    /// <summary>Exemplo de pergunta do cidadão e a resposta desejada (few-shot).</summary>
    Exemplo = 2,

    /// <summary>Glossário/termo local e seu significado.</summary>
    Glossario = 3,

    /// <summary>O que o robô DEVE fazer.</summary>
    Do = 4,

    /// <summary>O que o robô NÃO deve fazer.</summary>
    Dont = 5,
}

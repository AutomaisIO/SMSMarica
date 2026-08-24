namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Proporção/frame da imagem de assinatura do médico. Define o enquadramento
/// padronizado no upload e orienta o layout do carimbo no PDF do laudo (saber
/// se a rubrica é quadrada ou em faixa permite posicionar CRM/RQE ao lado ou
/// abaixo).
/// </summary>
public enum FormatoAssinaturaMedico
{
    /// <summary>Quadrada (1:1) — frame 800×800. Rubrica compacta.</summary>
    Quadrada = 1,

    /// <summary>Horizontal/faixa (2:1) — frame 800×400. Rubrica "deitada".</summary>
    Horizontal = 2,
}

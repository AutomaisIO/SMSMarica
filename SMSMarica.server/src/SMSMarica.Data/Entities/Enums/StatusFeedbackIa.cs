namespace SMSMarica.Data.Entities.Enums;

/// <summary>Estado de uma avaliação da Consulta Inteligente no fluxo de melhorias.</summary>
public enum StatusFeedbackIa
{
    /// <summary>Aguardando tratamento na tela de Melhorias de IA.</summary>
    Pendente = 0,

    /// <summary>Tratado — virou (ou reforçou) conhecimento da base/família.</summary>
    Tratado = 1,

    /// <summary>Descartado — não gera melhoria (ruído, duplicado, etc.).</summary>
    Descartado = 2,
}

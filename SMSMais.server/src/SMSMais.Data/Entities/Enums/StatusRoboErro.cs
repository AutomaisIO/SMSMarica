namespace SMSMais.Data.Entities.Enums;

/// <summary>Situação de um erro de resposta do robô marcado para treinamento. Valor estável.</summary>
public enum StatusRoboErro
{
    /// <summary>Marcado por um atendente, aguardando revisão de quem cuida do treinamento.</summary>
    Aberto = 1,

    /// <summary>Revisado (virou aprendizado / ajuste de treino).</summary>
    Revisado = 2,

    /// <summary>Descartado (não era erro ou não requer ação).</summary>
    Descartado = 3,
}

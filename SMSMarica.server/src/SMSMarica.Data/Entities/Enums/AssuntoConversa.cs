namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Assunto/categoria da conversa. O chat é transversal a todo o SMSMarica (TFD, marcação de
/// consulta, dúvidas, atendente geral). Opcional — <c>null</c> = ainda não classificado.
/// Serve para filtrar/organizar as filas; não gateia regras. Valor inteiro estável.
/// </summary>
public enum AssuntoConversa
{
    Tfd = 1,
    MarcacaoConsulta = 2,
    Duvida = 3,
    Atendente = 4,
    Outro = 99,
}

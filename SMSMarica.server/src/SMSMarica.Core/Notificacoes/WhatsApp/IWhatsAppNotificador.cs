namespace SMSMarica.Core.Notificacoes.WhatsApp;

/// <summary>
/// Mensagens do TFD ao paciente pelo WhatsApp (em modo simulado enquanto não há conta Meta).
/// </summary>
public interface IWhatsAppNotificador
{
    /// <summary>Pergunta ao paciente se haverá acompanhante (resposta 1=sim / 2=não tratada no webhook).</summary>
    Task PerguntarAcompanhanteAsync(Guid sessaoId, CancellationToken ct = default);

    /// <summary>Avisa o paciente do horário de coleta do transporte.</summary>
    Task AvisarColetaAsync(Guid sessaoId, CancellationToken ct = default);
}

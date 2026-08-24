namespace SMSMais.Core.Notificacoes.WhatsApp;

/// <summary>
/// Mensagens do TFD ao paciente pelo WhatsApp (simula quando o canal WhatsApp não está configurado).
/// </summary>
public interface IWhatsAppNotificador
{
    /// <summary>Pergunta ao paciente se haverá acompanhante (resposta 1=sim / 2=não tratada no webhook).</summary>
    Task PerguntarAcompanhanteAsync(Guid sessaoId, CancellationToken ct = default);

    /// <summary>Avisa o paciente do horário de coleta do transporte.</summary>
    Task AvisarColetaAsync(Guid sessaoId, CancellationToken ct = default);
}

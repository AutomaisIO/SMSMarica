using SMSMarica.Data.Entities.Conversas;
using SMSMarica.Data.Entities.Tfd;

namespace SMSMarica.Core.Notificacoes.WhatsApp.Manipuladores;

/// <summary>
/// Contexto de uma mensagem inbound já anexada a uma conversa, passado aos manipuladores
/// específicos de domínio (ex.: confirmação de acompanhante do TFD).
/// </summary>
public sealed record ManipuladorContexto(
    Conversa Conversa,
    MensagemWhatsApp Mensagem,
    string? Texto,
    Guid? PacienteId,
    // Payload do quick reply de template (messages[].button.payload) — roteia a resposta
    // de volta ao domínio (ex.: "confirma:{solicitacaoId}").
    string? BotaoPayload = null,
    // Id do botão escolhido numa mensagem interativa (interactive.button_reply.id).
    string? InterativoReplyId = null);

/// <summary>
/// Regra de negócio plugável disparada por uma mensagem recebida. O caminho principal do webhook
/// (abrir/atualizar conversa) roda ANTES; os manipuladores só reagem. NÃO devem chamar
/// <c>SaveChanges</c> — apenas mutam entidades rastreadas; o webhook faz o commit único.
/// Ordenados por <see cref="Ordem"/> (menor primeiro).
/// </summary>
public interface IManipuladorMensagemWhatsApp
{
    int Ordem { get; }
    Task TratarAsync(ManipuladorContexto ctx, CancellationToken ct);
}

using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Notificacoes;

/// <summary>
/// Pontos de extensão para notificar o cidadão (WhatsApp/push) ou sistemas
/// externos quando uma solicitação muda de estado. Implementação default
/// (<c>NotificadorExameLog</c>) só registra em log; integrações futuras
/// implementam essa mesma interface sem mexer nos services.
/// </summary>
public interface INotificadorExame
{
    Task NotificarAgendadoAsync(SolicitacaoExame solicitacao, CancellationToken cancellationToken = default);

    Task NotificarRealizadoAsync(SolicitacaoExame solicitacao, CancellationToken cancellationToken = default);
}

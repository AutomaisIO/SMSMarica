using Microsoft.Extensions.Logging;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Notificacoes;

/// <summary>
/// Implementação MVP que apenas registra em log. Quando integrar
/// WhatsApp/push, adicionar nova classe implementando <see cref="INotificadorExame"/>
/// e trocar o registro em <c>DependencyInjection</c>.
/// </summary>
public sealed class NotificadorExameLog(ILogger<NotificadorExameLog> logger) : INotificadorExame
{
    private readonly ILogger<NotificadorExameLog> _logger = logger;

    public Task NotificarAgendadoAsync(ExameImagem s, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[NOTIF] Solicitação {Accession} AGENDADA — paciente {PacienteId} aguarda execução.",
            s.AccessionNumber, s.Solicitacao!.PacienteId);
        return Task.CompletedTask;
    }

    public Task NotificarRealizadoAsync(ExameImagem s, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[NOTIF] Solicitação {Accession} REALIZADA — paciente {PacienteId} pode retirar resultado.",
            s.AccessionNumber, s.Solicitacao!.PacienteId);
        return Task.CompletedTask;
    }
}

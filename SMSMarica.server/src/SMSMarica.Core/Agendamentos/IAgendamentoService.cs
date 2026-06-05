using SMSMarica.Core.Agendamentos.Dtos;

namespace SMSMarica.Core.Agendamentos;

/// <summary>
/// Cálculo de horários livres e ciclo de vida do agendamento (marcar → confirmar →
/// realizar / faltar / cancelar). O paciente é validado no hub FHIR ao marcar. Ver ADR-0012.
/// </summary>
public interface IAgendamentoService
{
    /// <summary>Horários livres de uma agenda no intervalo [inicio, fim].</summary>
    Task<IReadOnlyList<SlotLivreDto>> CalcularHorariosLivresAsync(
        Guid agendaId, DateOnly inicio, DateOnly fim, CancellationToken cancellationToken = default);

    /// <summary>Agendamentos (não excluídos) de uma agenda no intervalo [inicio, fim].</summary>
    Task<IReadOnlyList<AgendamentoListItemDto>> ListarAsync(
        Guid agendaId, DateOnly inicio, DateOnly fim, CancellationToken cancellationToken = default);

    Task<AgendamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> AgendarAsync(AgendarRequest request, CancellationToken cancellationToken = default);

    Task ConfirmarAsync(Guid id, CancellationToken cancellationToken = default);
    Task RealizarAsync(Guid id, CancellationToken cancellationToken = default);
    Task RegistrarFaltaAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelarAsync(Guid id, CancelarAgendamentoRequest request, CancellationToken cancellationToken = default);
}

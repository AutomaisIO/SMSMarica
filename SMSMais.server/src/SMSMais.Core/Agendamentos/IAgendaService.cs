using SMSMais.Core.Agendamentos.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Agendamentos;

/// <summary>
/// Gestão de agendas (grade) e suas disponibilidades: recorrências semanais, janelas
/// avulsas e bloqueios. Médico e unidade são validados ao cadastrar. Ver ADR-0012.
/// </summary>
public interface IAgendaService
{
    Task<IReadOnlyList<AgendaListItemDto>> ListarAsync(
        FinalidadeAgenda? finalidade, Guid? unidadeId, Guid? especialidadeId, Guid? medicoId, Guid? equipamentoId,
        bool incluirInativas, CancellationToken cancellationToken = default);

    Task<AgendaDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(CadastrarAgendaRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarAgendaRequest request, CancellationToken cancellationToken = default);

    Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default);

    // ---- Recorrências ----
    Task<Guid> AdicionarRecorrenciaAsync(Guid agendaId, AdicionarRecorrenciaRequest request, CancellationToken cancellationToken = default);
    Task RemoverRecorrenciaAsync(Guid agendaId, Guid recorrenciaId, CancellationToken cancellationToken = default);

    // ---- Avulsos / Bloqueios ----
    Task<DisponibilidadesAgendaDto> ListarDisponibilidadesAsync(Guid agendaId, DateOnly inicio, DateOnly fim, CancellationToken cancellationToken = default);
    Task<Guid> AdicionarAvulsoAsync(Guid agendaId, AdicionarAvulsoRequest request, CancellationToken cancellationToken = default);
    Task RemoverAvulsoAsync(Guid agendaId, Guid avulsoId, CancellationToken cancellationToken = default);
    Task<Guid> AdicionarBloqueioAsync(Guid agendaId, AdicionarBloqueioRequest request, CancellationToken cancellationToken = default);
    Task RemoverBloqueioAsync(Guid agendaId, Guid bloqueioId, CancellationToken cancellationToken = default);
}

using SMSMarica.Core.Rastreamento.Dtos;

namespace SMSMarica.Core.Rastreamento;

public interface IRastreamentoService
{
    // Skeleton compat — mantido pra DependencyInjection existente.
    Task<IReadOnlyList<PontoGpsDto>> ListarAsync(CancellationToken cancellationToken = default);

    // Pontos GPS — append-only
    Task<Guid> RegistrarPontoAsync(RegistrarPontoGpsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PontoGpsDto>> ListarPontosPorMotoristaAsync(
        Guid motoristaId, DateTime? desde, DateTime? ate, CancellationToken cancellationToken = default);

    // Geofences — CRUD
    Task<IReadOnlyList<GeofenceDto>> ListarGeofencesAsync(CancellationToken cancellationToken = default);
    Task<GeofenceDto> ObterGeofencePorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarGeofenceAsync(CadastrarGeofenceRequest request, CancellationToken cancellationToken = default);
    Task AtualizarGeofenceAsync(Guid id, AtualizarGeofenceRequest request, CancellationToken cancellationToken = default);
    Task DeletarGeofenceAsync(Guid id, CancellationToken cancellationToken = default);

    // Eventos de chegada — read-only
    Task<IReadOnlyList<EventoChegadaDto>> ListarEventosPorRotaAsync(Guid rotaId, CancellationToken cancellationToken = default);

    // FT5 — pacientes aguardando retorno (fora de Maricá) + "puxar"
    Task<IReadOnlyList<PacienteAguardandoDto>> ListarAguardandoAsync(Guid? motoristaId, CancellationToken cancellationToken = default);
    Task MarcarAguardandoRetornoAsync(Guid sessaoId, CancellationToken cancellationToken = default);
    Task<Guid> PuxarAsync(PuxarPacienteRequest request, CancellationToken cancellationToken = default);
}

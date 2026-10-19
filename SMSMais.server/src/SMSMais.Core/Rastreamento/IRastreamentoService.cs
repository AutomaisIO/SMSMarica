using SMSMais.Core.Rastreamento.Dtos;

namespace SMSMais.Core.Rastreamento;

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

    // Mapa da frota ao vivo — snapshot do dia (rota + veículo + motorista + última posição)
    Task<IReadOnlyList<FrotaVeiculoDto>> ListarFrotaAsync(DateOnly? data, CancellationToken cancellationToken = default);

    // Resgate: a lista e a marcação ficam em Translado.Resgate.IResgateService. Isto é o aviso antigo
    // de "terminou o atendimento" (FT5), que agora vale como aviso da central de que está no local.
    Task MarcarAguardandoRetornoAsync(Guid sessaoId, CancellationToken cancellationToken = default);
}

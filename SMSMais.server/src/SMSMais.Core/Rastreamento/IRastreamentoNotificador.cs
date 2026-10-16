using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Rastreamento;

/// <summary>
/// Publica eventos de rastreamento em tempo real (SignalR). É uma abstração do Core;
/// a implementação concreta vive na Api (Hub). O padrão é no-op (testes/console),
/// sobrescrito na Api por <c>RastreamentoNotificadorSignalR</c>.
/// </summary>
public interface IRastreamentoNotificador
{
    Task PosicaoAtualizadaAsync(Guid motoristaId, double latitude, double longitude, DateTime capturadoEm, CancellationToken ct = default);
    Task RotaAtualizadaAsync(Guid rotaId, CancellationToken ct = default);

    /// <summary>REPORT do tablet do veículo (acidente, pane...): alerta a central em todo o painel.</summary>
    Task OcorrenciaRegistradaAsync(Guid ocorrenciaId, Guid veiculoId, string veiculoPlaca, TipoOcorrenciaVeiculo tipo, CancellationToken ct = default);

    /// <summary>Ocorrência atendida ou encerrada: as outras telas abertas atualizam a lista.</summary>
    Task OcorrenciaAtualizadaAsync(Guid ocorrenciaId, CancellationToken ct = default);
}

public sealed class NotificadorRastreamentoNulo : IRastreamentoNotificador
{
    public Task PosicaoAtualizadaAsync(Guid motoristaId, double latitude, double longitude, DateTime capturadoEm, CancellationToken ct = default) => Task.CompletedTask;
    public Task RotaAtualizadaAsync(Guid rotaId, CancellationToken ct = default) => Task.CompletedTask;
    public Task OcorrenciaRegistradaAsync(Guid ocorrenciaId, Guid veiculoId, string veiculoPlaca, TipoOcorrenciaVeiculo tipo, CancellationToken ct = default) => Task.CompletedTask;
    public Task OcorrenciaAtualizadaAsync(Guid ocorrenciaId, CancellationToken ct = default) => Task.CompletedTask;
}

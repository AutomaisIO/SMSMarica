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
}

public sealed class NotificadorRastreamentoNulo : IRastreamentoNotificador
{
    public Task PosicaoAtualizadaAsync(Guid motoristaId, double latitude, double longitude, DateTime capturadoEm, CancellationToken ct = default) => Task.CompletedTask;
    public Task RotaAtualizadaAsync(Guid rotaId, CancellationToken ct = default) => Task.CompletedTask;
}

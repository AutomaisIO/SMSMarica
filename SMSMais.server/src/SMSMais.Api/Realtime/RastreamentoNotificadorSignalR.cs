using Microsoft.AspNetCore.SignalR;
using SMSMais.Api.Hubs;
using SMSMais.Core.Rastreamento;

namespace SMSMais.Api.Realtime;

public sealed class RastreamentoNotificadorSignalR(IHubContext<RastreamentoHub> hub) : IRastreamentoNotificador
{
    public Task PosicaoAtualizadaAsync(Guid motoristaId, double latitude, double longitude, DateTime capturadoEm, CancellationToken ct = default) =>
        hub.Clients.Groups("painel", $"motorista:{motoristaId}")
            .SendAsync("posicaoAtualizada", new { motoristaId, latitude, longitude, capturadoEm }, ct);

    public Task RotaAtualizadaAsync(Guid rotaId, CancellationToken ct = default) =>
        hub.Clients.Groups("painel", $"rota:{rotaId}")
            .SendAsync("rotaAtualizada", new { rotaId }, ct);
}

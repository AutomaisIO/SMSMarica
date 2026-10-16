using Microsoft.AspNetCore.SignalR;
using SMSMais.Api.Hubs;
using SMSMais.Core.Rastreamento;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Realtime;

public sealed class RastreamentoNotificadorSignalR(IHubContext<RastreamentoHub> hub) : IRastreamentoNotificador
{
    public Task PosicaoAtualizadaAsync(Guid motoristaId, double latitude, double longitude, DateTime capturadoEm, CancellationToken ct = default) =>
        hub.Clients.Groups("painel", $"motorista:{motoristaId}")
            .SendAsync("posicaoAtualizada", new { motoristaId, latitude, longitude, capturadoEm }, ct);

    public Task RotaAtualizadaAsync(Guid rotaId, CancellationToken ct = default) =>
        hub.Clients.Groups("painel", $"rota:{rotaId}")
            .SendAsync("rotaAtualizada", new { rotaId }, ct);

    public Task OcorrenciaRegistradaAsync(Guid ocorrenciaId, Guid veiculoId, string veiculoPlaca, TipoOcorrenciaVeiculo tipo, CancellationToken ct = default) =>
        hub.Clients.Group("painel")
            .SendAsync("ocorrenciaRegistrada", new { ocorrenciaId, veiculoId, veiculoPlaca, tipo = tipo.ToString() }, ct);

    public Task OcorrenciaAtualizadaAsync(Guid ocorrenciaId, CancellationToken ct = default) =>
        hub.Clients.Group("painel")
            .SendAsync("ocorrenciaAtualizada", new { ocorrenciaId }, ct);
}

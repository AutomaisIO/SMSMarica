using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SMSMarica.Api.Hubs;

/// <summary>
/// Hub de tempo real do TFD: posição dos veículos, ETA, chegadas e atualizações de rota.
/// Grupos: <c>painel</c> (todos), <c>motorista:{id}</c>, <c>rota:{id}</c>, <c>paciente:{id}</c>.
/// Autenticação via JWT no query string (<c>access_token</c>) — ver Program.cs.
/// </summary>
[Authorize]
public sealed class RastreamentoHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Staff do painel recebe tudo; clientes específicos assinam seus grupos.
        await Groups.AddToGroupAsync(Context.ConnectionId, "painel");
        await base.OnConnectedAsync();
    }

    public Task AssinarRota(Guid rotaId) => Groups.AddToGroupAsync(Context.ConnectionId, $"rota:{rotaId}");
    public Task AssinarMotorista(Guid motoristaId) => Groups.AddToGroupAsync(Context.ConnectionId, $"motorista:{motoristaId}");
    public Task AssinarPaciente(Guid pacienteId) => Groups.AddToGroupAsync(Context.ConnectionId, $"paciente:{pacienteId}");
}

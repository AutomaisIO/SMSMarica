using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SMSMarica.Core.Conversas;

namespace SMSMarica.Api.Hubs;

/// <summary>
/// Hub de tempo real do chat (Central de Atendimento). Grupos: <c>usuario:{id}</c> (o próprio
/// operador), <c>unidade:{id}</c> (cada unidade que o operador cobre) e <c>conversa:{id}</c>
/// (thread aberta na tela). Autenticação via JWT no query string (<c>access_token</c>) — ver
/// Program.cs. Espelha <see cref="RastreamentoHub"/>.
/// </summary>
[Authorize]
public sealed class ConversasHub(IUsuarioUnidadeService vinculos) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var sub = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(sub, out var usuarioId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"usuario:{usuarioId}");

            var unidades = await vinculos.ObterUnidadeIdsAsync(usuarioId, Context.ConnectionAborted);
            foreach (var unidadeId in unidades)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"unidade:{unidadeId}");
        }

        await base.OnConnectedAsync();
    }

    /// <summary>Assina a thread aberta na tela (recebe cada mensagem em tempo real).</summary>
    public Task AssinarConversa(Guid conversaId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"conversa:{conversaId}");

    public Task DesassinarConversa(Guid conversaId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversa:{conversaId}");
}

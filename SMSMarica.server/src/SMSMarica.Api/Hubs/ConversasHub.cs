using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SMSMarica.Core.Conversas;
using SMSMarica.Core.Identidade;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Hubs;

/// <summary>
/// Hub de tempo real do chat (Central de Atendimento). Grupos: <c>usuario:{id}</c> (o próprio
/// operador), <c>unidade:{id}</c> (cada unidade que o operador cobre), <c>conversa:{id}</c>
/// (thread aberta na tela), <c>conversas:geral</c> (todo operador do chat — recebe eventos de
/// conversas sem unidade, visíveis a todos na lista) e <c>conversas:supervisao</c> (quem vê a
/// aba Todas). Autenticação via JWT no query string (<c>access_token</c>) — ver Program.cs.
/// Espelha <see cref="RastreamentoHub"/>.
/// </summary>
[Authorize]
public sealed class ConversasHub(IUsuarioUnidadeService vinculos, IIdentidadeService identidade) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var sub = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(sub, out var usuarioId))
        {
            // Gate único do chat: sem o módulo Conversas ("Central de Atendimento" no perfil) a
            // conexão fica sem NENHUM grupo — não recebe mensagem, badge nem notificação, e o
            // AssinarConversa é recusado. O gate do front (widget) é só cosmético; este é o real.
            var modulos = await ObterModulosAsync(usuarioId);
            if (modulos.Contains(ModuloPermissao.Conversas))
            {
                Context.Items[ChavePodeAtender] = true;

                await Groups.AddToGroupAsync(Context.ConnectionId, $"usuario:{usuarioId}");

                var unidades = await vinculos.ObterUnidadeIdsAsync(usuarioId, Context.ConnectionAborted);
                foreach (var unidadeId in unidades)
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"unidade:{unidadeId}");

                // Espelha a visibilidade da lista (ConversaService.ListarAsync): conversa sem
                // unidade é visível a todo operador do módulo; supervisor vê tudo. Sem esses
                // grupos, mensagem de número novo (sem operador/unidade) não chegava a ninguém.
                await Groups.AddToGroupAsync(Context.ConnectionId, "conversas:geral");
                if (modulos.Contains(ModuloPermissao.ConversasSupervisao))
                    await Groups.AddToGroupAsync(Context.ConnectionId, "conversas:supervisao");
            }
        }

        await base.OnConnectedAsync();
    }

    private const string ChavePodeAtender = "podeAtender";

    private async Task<IReadOnlySet<ModuloPermissao>> ObterModulosAsync(Guid usuarioId)
    {
        try
        {
            var perms = await identidade.ObterPermissoesResolvidasAsync(usuarioId, Context.ConnectionAborted);
            return perms.Resolvidas
                .Where(p => p.Acoes != AcoesPermissao.Nenhuma)
                .Select(p => p.Modulo)
                .ToHashSet();
        }
        catch
        {
            return new HashSet<ModuloPermissao>(); // usuário sumiu/sem permissões — sem grupos amplos
        }
    }

    /// <summary>Assina a thread aberta na tela (recebe cada mensagem em tempo real).</summary>
    public Task AssinarConversa(Guid conversaId) =>
        Context.Items.ContainsKey(ChavePodeAtender)
            ? Groups.AddToGroupAsync(Context.ConnectionId, $"conversa:{conversaId}")
            : Task.CompletedTask;

    public Task DesassinarConversa(Guid conversaId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversa:{conversaId}");
}

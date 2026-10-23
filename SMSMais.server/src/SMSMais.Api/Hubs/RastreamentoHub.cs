using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Hubs;

/// <summary>
/// Hub de tempo real do TFD para a EQUIPE: posição dos veículos, atualizações de rota e ocorrências.
/// Grupo <c>painel</c>: quem tem Consulta em Rastreamento (a mesma trava do painel para ligar o hub).
/// Os eventos de um motorista ou de uma rota também saem para <c>motorista:{id}</c> e
/// <c>rota:{id}</c>, que hoje ninguém assina.
/// Autenticação via JWT no query string (<c>access_token</c>) — ver Program.cs.
/// </summary>
/// <remarks>
/// O JWT do cidadão (app e PWA) é do mesmo esquema e passa no <c>[Authorize]</c>. Por isso a conexão
/// confere a permissão do usuário da equipe, como o <c>RequerPermissao</c> dos controllers, e derruba
/// qualquer outra. Até 23/10/2026 toda conexão entrava no grupo do painel e os métodos
/// <c>Assinar*</c> aceitavam qualquer id: um cidadão logado recebia os eventos da central. Se a
/// assinatura por rota, motorista ou paciente voltar, ela precisa conferir de quem é o id.
/// </remarks>
[Authorize]
public sealed class RastreamentoHub(IIdentidadeService identidade) : Hub
{
    public const string GrupoPainel = "painel";

    public override async Task OnConnectedAsync()
    {
        if (!await PodeAcompanharAsync())
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GrupoPainel);
        await base.OnConnectedAsync();
    }

    private async Task<bool> PodeAcompanharAsync()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated != true) return false;
        if (user.FindFirstValue("tipo") == "cidadao") return false;

        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var usuarioId)) return false;

        try
        {
            var permissoes = await identidade.ObterPermissoesResolvidasAsync(usuarioId, Context.ConnectionAborted);
            var rastreamento = permissoes.Resolvidas.FirstOrDefault(p => p.Modulo == ModuloPermissao.Rastreamento);
            return rastreamento is not null
                && (rastreamento.Acoes & AcoesPermissao.Consulta) == AcoesPermissao.Consulta;
        }
        catch (NaoEncontradoException)
        {
            // O sub não é de um usuário da equipe.
            return false;
        }
    }
}

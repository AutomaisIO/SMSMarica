using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SMSMais.Core.ComunicacaoInterna;
using SMSMais.Core.ComunicacaoInterna.Dtos;
using SMSMais.Core.Conversas;
using SMSMais.Core.Identidade;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Hubs;

/// <summary>
/// Hub da Comunicação interna (ADR-0079). Grupos: <c>ci:usuario:{id}</c> (a própria pessoa, em
/// todas as abas), <c>ci:unidade:{id}</c> (a unidade PADRÃO dela — só essa, ADR-0081) e <c>ci:todos</c>
/// (todo mundo com o módulo — recebe as mudanças de presença). O grupo de unidade é lido na
/// conexão: trocar a unidade padrão só vale depois que a aba reconecta. Também é o registro de presença:
/// cada conexão viva conta como "painel aberto". Autenticação via JWT no query string, como o
/// <see cref="ConversasHub"/>.
/// </summary>
[Authorize]
public sealed class ComunicacaoInternaHub(
    IUsuarioUnidadeService vinculos,
    IIdentidadeService identidade,
    IPresencaInterna presenca,
    IComunicacaoInternaService servico) : Hub
{
    public const string GrupoTodos = "ci:todos";
    public static string GrupoUsuario(Guid id) => $"ci:usuario:{id}";
    public static string GrupoUnidade(Guid id) => $"ci:unidade:{id}";

    private const string ChaveUsuario = "ciUsuario";

    /// <summary>Teto do "digitando…" por conexão — o front já espaça, isto protege o banco de um cliente com defeito.</summary>
    private static readonly TimeSpan IntervaloDigitando = TimeSpan.FromSeconds(2);
    private const string ChaveUltimoDigitando = "ciUltimoDigitando";

    public override async Task OnConnectedAsync()
    {
        var sub = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Gate real (o do front é cosmético): sem o módulo, a conexão fica sem grupo nenhum, não
        // conta como online e o Digitando é ignorado.
        if (Guid.TryParse(sub, out var usuarioId) && await TemModuloAsync(usuarioId))
        {
            Context.Items[ChaveUsuario] = usuarioId;
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoUsuario(usuarioId));
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoTodos);
            if (await vinculos.ObterPrincipalIdAsync(usuarioId, Context.ConnectionAborted) is { } unidadeId)
                await Groups.AddToGroupAsync(Context.ConnectionId, GrupoUnidade(unidadeId));

            if (presenca.Conectou(usuarioId, Context.ConnectionId))
                await Clients.Group(GrupoTodos).SendAsync("presencaInterna", new PresencaInternaEventoDto(usuarioId, true));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(ChaveUsuario, out var valor) && valor is Guid usuarioId
            && presenca.Desconectou(usuarioId, Context.ConnectionId))
        {
            await Clients.Group(GrupoTodos).SendAsync("presencaInterna", new PresencaInternaEventoDto(usuarioId, false));
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>"Fulano está digitando…" para a audiência da conversa (menos a própria conexão).</summary>
    public async Task Digitando(Guid conversaId)
    {
        if (!Context.Items.TryGetValue(ChaveUsuario, out var valor) || valor is not Guid usuarioId) return;

        var agora = DateTime.UtcNow;
        if (Context.Items.TryGetValue(ChaveUltimoDigitando, out var ultimo) && ultimo is DateTime antes
            && agora - antes < IntervaloDigitando)
        {
            return;
        }
        Context.Items[ChaveUltimoDigitando] = agora;

        var alvo = await servico.ObterAudienciaDigitandoAsync(usuarioId, conversaId, Context.ConnectionAborted);
        if (alvo is not { } a) return;

        // As outras abas da própria pessoa também recebem — o front ignora o evento quando é ela.
        var evt = new DigitandoInternoEventoDto(conversaId, usuarioId, a.Nome);
        var grupos = a.Audiencia.Unidades.Select(GrupoUnidade)
            .Concat(a.Audiencia.Usuarios.Select(GrupoUsuario))
            .Distinct();
        foreach (var grupo in grupos)
            await Clients.GroupExcept(grupo, Context.ConnectionId).SendAsync("digitandoInterno", evt);
    }

    private async Task<bool> TemModuloAsync(Guid usuarioId)
    {
        try
        {
            var perms = await identidade.ObterPermissoesResolvidasAsync(usuarioId, Context.ConnectionAborted);
            return perms.Resolvidas.Any(p =>
                p.Modulo == ModuloPermissao.ComunicacaoInterna
                && (p.Acoes & AcoesPermissao.Consulta) != AcoesPermissao.Nenhuma);
        }
        catch
        {
            return false; // usuário sumiu/sem permissões — sem grupos
        }
    }
}

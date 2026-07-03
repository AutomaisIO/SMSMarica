using Microsoft.AspNetCore.SignalR;
using SMSMarica.Api.Hubs;
using SMSMarica.Core.Conversas;

namespace SMSMarica.Api.Realtime;

/// <summary>
/// Implementação SignalR de <see cref="IConversaNotificador"/>. Roteia cada evento para os grupos
/// certos: a thread aberta (<c>conversa:{id}</c>), o operador dono (<c>usuario:{id}</c>) e a
/// unidade responsável (<c>unidade:{id}</c>). Espelha <c>RastreamentoNotificadorSignalR</c>.
/// </summary>
public sealed class ConversaNotificadorSignalR(IHubContext<ConversasHub> hub) : IConversaNotificador
{
    public Task MensagemRecebidaAsync(ConversaEventoRealtime evt, CancellationToken ct = default) =>
        hub.Clients.Groups(Grupos(evt)).SendAsync("mensagemRecebida", evt, ct);

    public Task MensagemEnviadaAsync(ConversaEventoRealtime evt, CancellationToken ct = default) =>
        hub.Clients.Groups(Grupos(evt)).SendAsync("mensagemEnviada", evt, ct);

    public Task ConversaAtualizadaAsync(ConversaEventoRealtime evt, CancellationToken ct = default) =>
        hub.Clients.Groups(Grupos(evt)).SendAsync("conversaAtualizada", evt, ct);

    private static IReadOnlyList<string> Grupos(ConversaEventoRealtime evt)
    {
        var grupos = new List<string>(3) { $"conversa:{evt.ConversaId}" };
        if (evt.OperadorResponsavelId is { } op) grupos.Add($"usuario:{op}");
        if (evt.UnidadeId is { } unidade) grupos.Add($"unidade:{unidade}");
        return grupos;
    }
}

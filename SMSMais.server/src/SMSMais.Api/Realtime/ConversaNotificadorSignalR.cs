using Microsoft.AspNetCore.SignalR;
using SMSMais.Api.Hubs;
using SMSMais.Core.Conversas;

namespace SMSMais.Api.Realtime;

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

    /// <summary>
    /// Mudança de posse/unidade: o mesmo <c>conversaAtualizada</c>, mas para a UNIÃO da audiência
    /// nova (a do evt) com a antiga — quem perdeu a conversa (dono anterior, fila anterior)
    /// precisa do evento para o invalidate tirá-la da própria lista.
    /// </summary>
    public Task ConversaMovidaAsync(
        ConversaEventoRealtime evt, Guid? deOperadorId, Guid? deUnidadeId, CancellationToken ct = default)
    {
        var grupos = new HashSet<string>(Grupos(evt));
        if (deOperadorId is { } op) grupos.Add($"usuario:{op}");
        if (deUnidadeId is { } unidade) grupos.Add($"unidade:{unidade}");
        else grupos.Add("conversas:geral");
        return hub.Clients.Groups([.. grupos]).SendAsync("conversaAtualizada", evt, ct);
    }

    /// <summary>
    /// Espelha a visibilidade da lista (ConversaService.ListarAsync — mudou lá? muda AQUI):
    /// conversa COM dono é da lista pessoal dele — só o dono (+ supervisão + thread aberta)
    /// recebe, sem bipar a unidade inteira; SEM dono é a fila — membros da unidade, ou todo
    /// operador do chat quando não há unidade (<c>conversas:geral</c>). Sem o fallback do
    /// geral, mensagem de número novo (operador e unidade nulos) não chegava a ninguém.
    /// </summary>
    private static IReadOnlyList<string> Grupos(ConversaEventoRealtime evt)
    {
        var grupos = new List<string>(3) { $"conversa:{evt.ConversaId}", "conversas:supervisao" };
        if (evt.OperadorResponsavelId is { } op) grupos.Add($"usuario:{op}");
        else if (evt.UnidadeId is { } unidade) grupos.Add($"unidade:{unidade}");
        else grupos.Add("conversas:geral");
        return grupos;
    }
}

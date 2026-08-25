namespace SMSMais.Core.Conversas;

/// <summary>
/// Payload de evento de conversa para o tempo real (SignalR). Carrega os dados de roteamento
/// (operador/unidade) e uma prévia para a UI decidir badge/notificação sem outra chamada.
/// </summary>
public sealed record ConversaEventoRealtime(
    Guid ConversaId,
    Guid? OperadorResponsavelId,
    Guid? UnidadeId,
    string TelefoneCanonical,
    string? NomeContato,
    string? Preview,
    int NaoLidas,
    DateTime? OcorridoEm);

/// <summary>
/// Publica eventos do chat em tempo real (SignalR). Abstração do Core; a implementação concreta
/// vive na Api (<c>ConversaNotificadorSignalR</c>). O padrão é no-op (testes/console/background),
/// sobrescrito na Api. Espelha <see cref="Rastreamento.IRastreamentoNotificador"/>.
/// </summary>
public interface IConversaNotificador
{
    /// <summary>Mensagem recebida do cidadão (inbound) — dispara badge/alerta nos operadores.</summary>
    Task MensagemRecebidaAsync(ConversaEventoRealtime evt, CancellationToken ct = default);

    /// <summary>Mensagem enviada por um operador (outbound) — atualiza quem está com a thread aberta.</summary>
    Task MensagemEnviadaAsync(ConversaEventoRealtime evt, CancellationToken ct = default);

    /// <summary>Conversa mudou de estado/atribuição/unidade/não-lidas — atualiza as listas.</summary>
    Task ConversaAtualizadaAsync(ConversaEventoRealtime evt, CancellationToken ct = default);

    /// <summary>
    /// Posse ou unidade mudou (assumir/devolver/encaminhar/transferir). Além da audiência NOVA
    /// (a do <paramref name="evt"/>), notifica a ANTIGA — o dono/fila de onde a conversa saiu
    /// precisa do evento para removê-la da própria lista.
    /// </summary>
    /// <param name="deOperadorId">Responsável anterior (null = estava na fila).</param>
    /// <param name="deUnidadeId">Unidade anterior (null = estava na triagem geral).</param>
    Task ConversaMovidaAsync(
        ConversaEventoRealtime evt, Guid? deOperadorId, Guid? deUnidadeId, CancellationToken ct = default);
}

public sealed class NotificadorConversaNulo : IConversaNotificador
{
    public Task MensagemRecebidaAsync(ConversaEventoRealtime evt, CancellationToken ct = default) => Task.CompletedTask;
    public Task MensagemEnviadaAsync(ConversaEventoRealtime evt, CancellationToken ct = default) => Task.CompletedTask;
    public Task ConversaAtualizadaAsync(ConversaEventoRealtime evt, CancellationToken ct = default) => Task.CompletedTask;
    public Task ConversaMovidaAsync(
        ConversaEventoRealtime evt, Guid? deOperadorId, Guid? deUnidadeId, CancellationToken ct = default) =>
        Task.CompletedTask;
}

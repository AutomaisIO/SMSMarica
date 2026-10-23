using SMSMais.Core.ComunicacaoInterna.Dtos;

namespace SMSMais.Core.ComunicacaoInterna;

/// <summary>
/// Quem recebe um evento: os grupos das unidades (conversa de unidade — todos os vinculados) e/ou
/// os grupos pessoais (conversa direta, e o próprio autor nas outras abas dele).
/// </summary>
public sealed record AudienciaInterna(IReadOnlyCollection<Guid> Unidades, IReadOnlyCollection<Guid> Usuarios);

/// <summary>
/// Publica os eventos da Comunicação interna em tempo real. Abstração do Core; a implementação
/// SignalR vive na Api (<c>ComunicacaoInternaNotificadorSignalR</c>). O padrão é no-op
/// (testes/background). Espelha <see cref="Conversas.IConversaNotificador"/>.
/// </summary>
public interface IComunicacaoInternaNotificador
{
    Task MensagemAsync(MensagemInternaEventoDto evt, AudienciaInterna audiencia, CancellationToken ct = default);

    Task LeituraAsync(LeituraInternaEventoDto evt, AudienciaInterna audiencia, CancellationToken ct = default);

    Task TranscricaoAsync(TranscricaoInternaEventoDto evt, AudienciaInterna audiencia, CancellationToken ct = default);
}

public sealed class NotificadorComunicacaoInternaNulo : IComunicacaoInternaNotificador
{
    public Task MensagemAsync(MensagemInternaEventoDto evt, AudienciaInterna audiencia, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task LeituraAsync(LeituraInternaEventoDto evt, AudienciaInterna audiencia, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task TranscricaoAsync(TranscricaoInternaEventoDto evt, AudienciaInterna audiencia, CancellationToken ct = default) =>
        Task.CompletedTask;
}

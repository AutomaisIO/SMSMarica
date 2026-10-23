using Microsoft.AspNetCore.SignalR;
using SMSMais.Api.Hubs;
using SMSMais.Core.ComunicacaoInterna;
using SMSMais.Core.ComunicacaoInterna.Dtos;

namespace SMSMais.Api.Realtime;

/// <summary>
/// Implementação SignalR de <see cref="IComunicacaoInternaNotificador"/>: conversa de unidade vai
/// para os grupos das duas unidades; direta, para os grupos pessoais. Uma conexão em mais de um
/// grupo recebe uma cópia por grupo — o front deduplica pelo id da mensagem.
/// </summary>
public sealed class ComunicacaoInternaNotificadorSignalR(IHubContext<ComunicacaoInternaHub> hub)
    : IComunicacaoInternaNotificador
{
    public Task MensagemAsync(MensagemInternaEventoDto evt, AudienciaInterna audiencia, CancellationToken ct = default) =>
        hub.Clients.Groups(Grupos(audiencia)).SendAsync("mensagemInterna", evt, ct);

    public Task LeituraAsync(LeituraInternaEventoDto evt, AudienciaInterna audiencia, CancellationToken ct = default) =>
        hub.Clients.Groups(Grupos(audiencia)).SendAsync("leituraInterna", evt, ct);

    private static IReadOnlyList<string> Grupos(AudienciaInterna a) =>
        a.Unidades.Select(ComunicacaoInternaHub.GrupoUnidade)
            .Concat(a.Usuarios.Select(ComunicacaoInternaHub.GrupoUsuario))
            .Distinct()
            .ToList();
}

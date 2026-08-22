using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Disparo de mensagens do TFD ao paciente pelo WhatsApp (em modo simulado enquanto não há
/// conexão com o Automais.Zap — o envio é registrado/logado e os fluxos funcionam ponta a ponta).
/// </summary>
[ApiController]
[Route("tfd/notificacoes")]
public sealed class NotificacoesTfdController(IWhatsAppNotificador notificador) : ControllerBase
{
    /// <summary>Pergunta ao paciente, por WhatsApp, se haverá acompanhante na sessão.</summary>
    [HttpPost("acompanhante/{sessaoId:guid}")]
    [RequerPermissao(ModuloPermissao.Translados, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PerguntarAcompanhante(Guid sessaoId, CancellationToken cancellationToken)
    {
        await notificador.PerguntarAcompanhanteAsync(sessaoId, cancellationToken);
        return NoContent();
    }

    /// <summary>Avisa o paciente, por WhatsApp, do horário de coleta do transporte.</summary>
    [HttpPost("coleta/{sessaoId:guid}")]
    [RequerPermissao(ModuloPermissao.Translados, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AvisarColeta(Guid sessaoId, CancellationToken cancellationToken)
    {
        await notificador.AvisarColetaAsync(sessaoId, cancellationToken);
        return NoContent();
    }
}

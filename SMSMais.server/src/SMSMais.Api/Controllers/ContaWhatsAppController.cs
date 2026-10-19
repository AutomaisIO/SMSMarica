using Microsoft.AspNetCore.Mvc;
using SMSMais.Core.Notificacoes.WhatsApp.Conta;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Faixa vermelha de "WhatsApp parado" no topo do painel. Qualquer usuário logado pergunta (o
/// painel chama de minuto em minuto); só quem resolve recebe o conteúdo — os demais recebem
/// <c>mostrar=false</c>. Ver <see cref="FaixaContaWhatsAppService"/>.
/// </summary>
[ApiController]
[Route("sistema/whatsapp-conta")]
public sealed class ContaWhatsAppController(IFaixaContaWhatsAppService service) : ControllerBase
{
    [HttpGet("faixa")]
    [ProducesResponseType<FaixaContaWhatsAppDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<FaixaContaWhatsAppDto> Faixa(CancellationToken cancellationToken) =>
        service.ObterAsync(cancellationToken);
}

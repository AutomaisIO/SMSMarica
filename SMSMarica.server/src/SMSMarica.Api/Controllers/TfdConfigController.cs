using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Tfd.Configuracao;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>Configuração das integrações externas (Google Maps, WhatsApp via Automais.Zap).</summary>
[ApiController]
[Route("integracoes/tfd")]
public sealed class TfdConfigController(ITfdConfigService service) : ControllerBase
{
    private readonly ITfdConfigService _service = service;

    [HttpGet("google")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Consulta)]
    [ProducesResponseType<TfdConfigGoogleDto>(StatusCodes.Status200OK)]
    public async Task<TfdConfigGoogleDto> ObterGoogle(CancellationToken cancellationToken) =>
        await _service.ObterGoogleAsync(cancellationToken);

    [HttpPut("google")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarGoogle(
        [FromBody] AtualizarTfdConfigGoogleRequest request, CancellationToken cancellationToken)
    {
        await _service.AtualizarGoogleAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("whatsapp")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Consulta)]
    [ProducesResponseType<TfdConfigWhatsAppDto>(StatusCodes.Status200OK)]
    public async Task<TfdConfigWhatsAppDto> ObterWhatsApp(CancellationToken cancellationToken) =>
        await _service.ObterWhatsAppAsync(cancellationToken);

    [HttpPut("whatsapp")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarWhatsApp(
        [FromBody] AtualizarTfdConfigWhatsAppRequest request, CancellationToken cancellationToken)
    {
        await _service.AtualizarWhatsAppAsync(request, cancellationToken);
        return NoContent();
    }
}

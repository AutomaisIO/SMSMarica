using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Estatisticas;
using SMSMarica.Core.Estatisticas.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Estatísticas gerenciais de atendimento (retrato das comunicações WhatsApp). Visão global,
/// só leitura — protegida por <see cref="ModuloPermissao.Estatistica"/>.
/// </summary>
[ApiController]
[Route("estatisticas")]
public sealed class EstatisticasController(IEstatisticasService service) : ControllerBase
{
    /// <summary>
    /// Retrato do WhatsApp no período. Sem <c>de</c>/<c>ate</c>, usa os últimos 30 dias.
    /// </summary>
    [HttpGet("whatsapp")]
    [RequerPermissao(ModuloPermissao.Estatistica, AcoesPermissao.Consulta)]
    [ProducesResponseType<EstatisticasWhatsAppDto>(StatusCodes.Status200OK)]
    public async Task<EstatisticasWhatsAppDto> WhatsApp(
        [FromQuery] DateOnly? de = null,
        [FromQuery] DateOnly? ate = null,
        CancellationToken ct = default)
    {
        var fim = ate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = de ?? fim.AddDays(-29);
        return await service.ObterWhatsAppAsync(inicio, fim, ct);
    }
}

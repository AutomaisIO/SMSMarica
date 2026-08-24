using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Inteligencia;
using SMSMarica.Core.Inteligencia.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>Bases ativas do módulo IA (consumidas pela Consulta Inteligente e pela configuração).</summary>
[ApiController]
[Route("ia")]
public sealed class IaController(IIaService service) : ControllerBase
{
    private readonly IIaService _service = service;

    [HttpGet("fontes")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<FonteResumoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<FonteResumoDto>> ListarFontesAtivas(CancellationToken cancellationToken) =>
        await _service.ListarFontesAtivasAsync(cancellationToken);
}

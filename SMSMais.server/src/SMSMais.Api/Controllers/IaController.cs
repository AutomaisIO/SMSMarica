using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Inteligencia;
using SMSMais.Core.Inteligencia.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

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

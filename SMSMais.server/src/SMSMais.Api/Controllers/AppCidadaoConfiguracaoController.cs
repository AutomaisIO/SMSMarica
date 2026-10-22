using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMais.Core.Cidadao.Entrada;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Sistema → App do cidadão (ADR-0078): a entrada pelo PWA e pelo app nativo, configuradas em separado.
/// Só admin global — conferido no serviço a cada chamada (403 para os demais). Cada ajuste fica na
/// trilha de auditoria e volta na própria resposta (<c>ajustes</c>).
/// </summary>
[ApiController]
[Authorize]
[Route("app-cidadao/configuracao")]
public sealed class AppCidadaoConfiguracaoController(IConfiguracaoAppCidadaoService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ConfiguracaoAppCidadaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<ConfiguracaoAppCidadaoDto> Obter(CancellationToken cancellationToken) =>
        service.ObterAsync(cancellationToken);

    [HttpPut]
    [ProducesResponseType<ConfiguracaoAppCidadaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<ConfiguracaoAppCidadaoDto> Atualizar(
        [FromBody] AtualizarConfiguracaoAppCidadaoRequest request, CancellationToken cancellationToken) =>
        service.AtualizarAsync(request, cancellationToken);
}

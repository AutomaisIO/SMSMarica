using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.ApiTokens;
using SMSMais.Core.ApiTokens.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Gestão dos tokens de API (chaves de serviço) usados por integrações externas
/// — ex.: o CentralIA chamando <c>/integracoes</c>. O token em claro só aparece
/// na resposta da criação; depois só restam prefixo e metadados. Revogação é
/// manual (sem expiração automática).
/// </summary>
[ApiController]
[Route("api-tokens")]
public sealed class ApiTokensController(IApiTokensService service) : ControllerBase
{
    private readonly IApiTokensService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.ApiTokens, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ApiTokenListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ApiTokenListItemDto>> Listar(CancellationToken cancellationToken) =>
        await _service.ListarAsync(cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.ApiTokens, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ApiTokenCriadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarApiTokenRequest request,
        CancellationToken cancellationToken)
    {
        var criado = await _service.CriarAsync(request, ObterUsuarioId(), cancellationToken);
        return CreatedAtAction(nameof(Listar), new { id = criado.Id }, criado);
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.ApiTokens, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revogar(Guid id, CancellationToken cancellationToken)
    {
        await _service.RevogarAsync(id, ObterUsuarioId(), cancellationToken);
        return NoContent();
    }

    private Guid? ObterUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

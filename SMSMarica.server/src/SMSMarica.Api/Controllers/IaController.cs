using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Inteligencia;
using SMSMarica.Core.Inteligencia.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>Tela de perguntar: envia a pergunta às bases selecionadas e lista as bases ativas.</summary>
[ApiController]
[Route("ia")]
public sealed class IaController(IIaService service) : ControllerBase
{
    private readonly IIaService _service = service;

    [HttpPost("perguntar")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    [ProducesResponseType<PerguntarRespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<PerguntarRespostaDto> Perguntar(
        [FromBody] PerguntarRequest request,
        CancellationToken cancellationToken) =>
        await _service.PerguntarAsync(request, cancellationToken);

    [HttpGet("fontes")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<FonteResumoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<FonteResumoDto>> ListarFontesAtivas(CancellationToken cancellationToken) =>
        await _service.ListarFontesAtivasAsync(cancellationToken);
}

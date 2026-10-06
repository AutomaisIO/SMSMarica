using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.ElevenLabs;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>Configuração da integração ElevenLabs (fala-para-texto do Agente IA).</summary>
[ApiController]
[Route("integracoes/elevenlabs")]
public sealed class ElevenLabsConfigController(IElevenLabsConfigService service) : ControllerBase
{
    private readonly IElevenLabsConfigService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Consulta)]
    [ProducesResponseType<ElevenLabsConfigDto>(StatusCodes.Status200OK)]
    public async Task<ElevenLabsConfigDto> Obter(CancellationToken cancellationToken) =>
        await _service.ObterAsync(cancellationToken);

    [HttpPut]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Atualizar(
        [FromBody] AtualizarElevenLabsConfigRequest request, CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(request, cancellationToken);
        return NoContent();
    }
}

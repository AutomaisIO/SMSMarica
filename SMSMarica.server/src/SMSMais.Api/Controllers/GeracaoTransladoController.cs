using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Translado.Geracao;
using SMSMais.Core.Translado.Geracao.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>Geração inteligente do translado do dia (FT3): distribui pacientes nos carros e otimiza as rotas.</summary>
[ApiController]
[Route("translados")]
public sealed class GeracaoTransladoController(IGeradorDeTransladoService service) : ControllerBase
{
    /// <summary>Gera o translado de uma data. <c>confirmar=false</c> só simula (preview); <c>true</c> grava as rotas.</summary>
    [HttpPost("gerar")]
    [RequerPermissao(ModuloPermissao.Translados, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ResultadoGeracaoDto>(StatusCodes.Status200OK)]
    public async Task<ResultadoGeracaoDto> Gerar([FromBody] GerarTransladoRequest request, CancellationToken cancellationToken) =>
        await service.GerarAsync(request, cancellationToken);
}

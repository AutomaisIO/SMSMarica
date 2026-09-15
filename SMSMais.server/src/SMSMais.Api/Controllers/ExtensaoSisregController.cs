using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Extensao;
using SMSMais.Core.Extensao.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Ingestão das operações que a extensão de navegador observa no SISREG (envio e retorno).
/// Fase de análise: o backend só armazena — não interpreta nem cria solicitação a partir daqui.
/// </summary>
[ApiController]
[Route("extensao/sisreg")]
public sealed class ExtensaoSisregController(IExtensaoCapturaService capturas) : ControllerBase
{
    /// <summary>
    /// Recebe um lote de capturas. Autenticado com o mesmo login do painel; exige a permissão
    /// da extensão. O corpo pode chegar comprimido (<c>Content-Encoding: gzip</c>) — a
    /// descompressão é tratada pelo middleware.
    /// </summary>
    [HttpPost("capturas")]
    [RequerPermissao(ModuloPermissao.ExtensaoSisreg, AcoesPermissao.Inclusao)]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType<CapturaLoteResultado>(StatusCodes.Status200OK)]
    public Task<CapturaLoteResultado> Receber(
        [FromBody] CapturaLoteRequest lote, CancellationToken cancellationToken)
        => capturas.ReceberAsync(lote, cancellationToken);
}

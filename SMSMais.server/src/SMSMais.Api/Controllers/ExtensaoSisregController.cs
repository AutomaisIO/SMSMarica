using Microsoft.AspNetCore.Mvc;
using SMSMais.Core.Extensao;
using SMSMais.Core.Extensao.Dtos;

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
    /// Recebe um lote de capturas. Basta estar autenticado no SMSMarica — sem permissão de
    /// módulo: qualquer usuário que rode a extensão envia (o <c>usuario_id</c> é carimbado a
    /// partir do token). O corpo pode chegar comprimido (<c>Content-Encoding: gzip</c>) — a
    /// descompressão é tratada pelo middleware.
    /// </summary>
    [HttpPost("capturas")]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType<CapturaLoteResultado>(StatusCodes.Status200OK)]
    public Task<CapturaLoteResultado> Receber(
        [FromBody] CapturaLoteRequest lote, CancellationToken cancellationToken)
        => capturas.ReceberAsync(lote, cancellationToken);
}

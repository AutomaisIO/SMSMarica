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

    /// <summary>Panorama do que já chegou (contagens/metadados, sem PII de paciente) — monitor
    /// do piloto. Basta estar autenticado.</summary>
    [HttpGet("capturas/resumo")]
    [ProducesResponseType<CapturaResumoDto>(StatusCodes.Status200OK)]
    public Task<CapturaResumoDto> Resumo(CancellationToken cancellationToken)
        => capturas.ObterResumoAsync(cancellationToken);

    /// <summary>Estrutura das ações de escrita (nomes de campos do envio + rótulos da resposta),
    /// sem PII — para decidir o que dá para montar na base a partir das capturas.</summary>
    [HttpGet("capturas/estrutura")]
    [ProducesResponseType<CapturaEstruturaDto>(StatusCodes.Status200OK)]
    public Task<CapturaEstruturaDto> Estrutura(CancellationToken cancellationToken)
        => capturas.ObterEstruturaAsync(cancellationToken);

    /// <summary>Trechos redigidos (sem PII) da resposta de uma marcação — para desenhar o parser.</summary>
    [HttpGet("capturas/amostra-resposta")]
    [ProducesResponseType<CapturaAmostraRespostaDto>(StatusCodes.Status200OK)]
    public Task<CapturaAmostraRespostaDto> AmostraResposta(CancellationToken cancellationToken)
        => capturas.ObterAmostraRespostaMarcacaoAsync(cancellationToken);
}

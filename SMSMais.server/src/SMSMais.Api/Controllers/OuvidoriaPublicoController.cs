using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMais.Core.Ouvidoria;
using SMSMais.Core.Ouvidoria.Dtos;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Canal público da ouvidoria (ADR-0060, plano §3.2): o cidadão registra sem login e acompanha
/// por protocolo + código de acesso. Anônimo por natureza; a defesa é o rate limit por IP
/// (<c>ouvidoria-publico</c>) e a comparação do código em tempo constante. Protocolo/código que
/// não batem devolvem o mesmo 404 — não se diz qual dos dois errou.
/// </summary>
[ApiController]
[Route("publico/ouvidoria")]
[AllowAnonymous]
[EnableRateLimiting("ouvidoria-publico")]
public sealed class OuvidoriaPublicoController(IOuvidoriaPublicoService servico) : ControllerBase
{
    /// <summary>Assuntos ativos para o formulário público (id, nome, pai).</summary>
    [HttpGet("assuntos")]
    [ProducesResponseType<IReadOnlyList<AssuntoPublicoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AssuntoPublicoDto>> Assuntos(CancellationToken ct)
    {
        Response.Headers.CacheControl = "public, max-age=300";
        return await servico.ListarAssuntosAsync(ct);
    }

    /// <summary>Unidades de saúde ativas e não externas (id, nome).</summary>
    [HttpGet("unidades")]
    [ProducesResponseType<IReadOnlyList<UnidadePublicaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UnidadePublicaDto>> Unidades(CancellationToken ct)
    {
        Response.Headers.CacheControl = "public, max-age=300";
        return await servico.ListarUnidadesAsync(ct);
    }

    /// <summary>Registra a manifestação pelo site. O código de acesso volta <b>só aqui</b>, uma única vez.</summary>
    [HttpPost("manifestacoes")]
    [ProducesResponseType<ManifestacaoCriadaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Registrar([FromBody] RegistrarManifestacaoPublicaRequest req, CancellationToken ct)
    {
        var criado = await servico.RegistrarAsync(req, ct);
        return CreatedAtAction(nameof(Acompanhar), new { protocolo = criado.Protocolo }, criado);
    }

    /// <summary>Acompanhamento por protocolo + código: status, prazo e só os eventos visíveis ao cidadão.</summary>
    [HttpGet("manifestacoes/{protocolo}")]
    [ProducesResponseType<AcompanhamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AcompanhamentoDto> Acompanhar(string protocolo, [FromQuery] string? codigo, CancellationToken ct)
        => await servico.AcompanharAsync(protocolo, codigo ?? string.Empty, ct);

    /// <summary>Cidadão envia a complementação pedida (só em <c>AguardandoComplementacao</c>).</summary>
    [HttpPost("manifestacoes/{protocolo}/complementar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complementar(string protocolo, [FromQuery] string? codigo, [FromBody] TextoRequest req, CancellationToken ct)
    {
        await servico.ComplementarAsync(protocolo, codigo ?? string.Empty, req.Texto, ct);
        return NoContent();
    }

    /// <summary>Cidadão recorre da resposta conclusiva (só em <c>Respondida</c>, uma vez).</summary>
    [HttpPost("manifestacoes/{protocolo}/recurso")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Recorrer(string protocolo, [FromQuery] string? codigo, [FromBody] TextoRequest req, CancellationToken ct)
    {
        await servico.RecorrerAsync(protocolo, codigo ?? string.Empty, req.Texto, ct);
        return NoContent();
    }
}

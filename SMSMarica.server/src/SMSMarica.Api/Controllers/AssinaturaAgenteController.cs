using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMarica.Core.Laudos.Assinatura;
using SMSMarica.Core.Laudos.Assinatura.Dtos;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Canal do agente local de assinatura. Autenticado pela <b>chave de uso único</b>
/// (gerada no "iniciar" e entregue ao agente via <c>automais-assinador://...?chave=</c>),
/// não por JWT/X-API-Key — por isso <see cref="AllowAnonymousAttribute"/>. A chave é
/// curta, atada ao job + médico, e expira em minutos.
/// </summary>
[ApiController]
[Route("assinatura/agente")]
[AllowAnonymous]
[EnableRateLimiting("agente-assinatura")]
public sealed class AssinaturaAgenteController(ILaudoAssinaturaService assinatura) : ControllerBase
{
    /// <summary>Reivindica o job pela chave; recebe o CPF do médico (para achar o certificado) e o título.</summary>
    [HttpPost("reivindicar")]
    [ProducesResponseType<ReivindicarResultado>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ReivindicarResultado> Reivindicar(
        [FromBody] ChaveRequest request, CancellationToken cancellationToken) =>
        await assinatura.ReivindicarAsync(request.Chave, cancellationToken);

    /// <summary>Envia o certificado escolhido; recebe o hash a assinar.</summary>
    [HttpPost("preparar")]
    [ProducesResponseType<PrepararJobResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<PrepararJobResultadoDto> Preparar(
        [FromBody] PrepararAgenteRequest request, CancellationToken cancellationToken)
    {
        var cadeia = (request.CadeiaCertificadoBase64 ?? [])
            .Select(Convert.FromBase64String)
            .ToArray();
        return await assinatura.PrepararAsync(request.Chave, cadeia, cancellationToken);
    }

    /// <summary>Envia a assinatura crua; o servidor embute o CMS e conclui.</summary>
    [HttpPost("concluir")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Concluir(
        [FromBody] ConcluirAgenteRequest request, CancellationToken cancellationToken)
    {
        await assinatura.ConcluirAsync(request.Chave, Convert.FromBase64String(request.RawSignatureBase64), cancellationToken);
        return NoContent();
    }
}

public sealed record ChaveRequest(string Chave);

public sealed record PrepararAgenteRequest(string Chave, IReadOnlyList<string>? CadeiaCertificadoBase64);

public sealed record ConcluirAgenteRequest(string Chave, string RawSignatureBase64);

using Automais.Assinador.Core.Pades;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Assinador.Api.Controllers;

/// <summary>
/// Operações sobre PDF que não são assinatura. Hoje só o carimbo sem certificado: o médico
/// que não tem certificado digital libera o laudo com a rubrica estampada, sem CMS.
/// </summary>
[ApiController]
[Route("pdf")]
public sealed class PdfController(IPadesSigner signer) : ControllerBase
{
    /// <summary>Estampa o carimbo (PNG) na página/retângulo informados e devolve o PDF.</summary>
    [HttpPost("carimbar")]
    public CarimbarResponse Carimbar([FromBody] CarimbarRequest req)
    {
        var posicao = req.Posicao is { } p
            ? new CarimboPosicao(p.Pagina, p.X, p.Y, p.Largura, p.Altura)
            : null;
        var pdf = signer.Carimbar(
            Convert.FromBase64String(req.PdfBase64),
            Convert.FromBase64String(req.CarimboPngBase64),
            posicao);
        return new CarimbarResponse(Convert.ToBase64String(pdf));
    }
}

public sealed record CarimbarRequest(string PdfBase64, string CarimboPngBase64, CarimboPosicaoRequest? Posicao = null);

public sealed record CarimbarResponse(string PdfBase64);

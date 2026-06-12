using Automais.Assinador.Core.Pades;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Assinador.Api.Controllers;

/// <summary>
/// Assinatura PAdES diferida (two-step). Stateless: o estado entre preparar e
/// concluir volta ao chamador no <c>transferState</c> (base64). A chave privada
/// nunca chega aqui — só o hash a assinar sai e a assinatura crua entra.
/// </summary>
[ApiController]
[Route("pades")]
public sealed class PadesController(IPadesSigner signer) : ControllerBase
{
    /// <summary>Reserva o placeholder + carimbo visual e devolve o hash a ser assinado.</summary>
    [HttpPost("preparar")]
    public PrepararResponse Preparar([FromBody] PrepararRequest req)
    {
        var carimboPng = string.IsNullOrWhiteSpace(req.CarimboPngBase64)
            ? null
            : Convert.FromBase64String(req.CarimboPngBase64);
        var visual = new CarimboVisual(req.NomeMedico, req.Crm, req.UfCrm, req.Rqe, req.TextoRodape, carimboPng);
        var resultado = signer.Preparar(new PreparacaoRequisicao(
            Convert.FromBase64String(req.PdfBase64),
            [.. req.CadeiaCertificadoBase64.Select(Convert.FromBase64String)],
            visual));

        return new PrepararResponse(
            Convert.ToBase64String(resultado.ToSignHash),
            resultado.AlgoritmoHash,
            Convert.ToBase64String(resultado.TransferState));
    }

    /// <summary>Injeta a assinatura crua no PDF preparado e devolve o assinado.</summary>
    [HttpPost("concluir")]
    public ConcluirResponse Concluir([FromBody] ConcluirRequest req)
    {
        var resultado = signer.Concluir(new ConclusaoRequisicao(
            Convert.FromBase64String(req.TransferStateBase64),
            Convert.FromBase64String(req.RawSignatureBase64)));

        return new ConcluirResponse(
            Convert.ToBase64String(resultado.PdfAssinado),
            resultado.Formato,
            resultado.CertificadoTitular,
            resultado.CertificadoEmissor,
            resultado.CpfTitular);
    }
}

public sealed record PrepararRequest(
    string PdfBase64,
    IReadOnlyList<string> CadeiaCertificadoBase64,
    string NomeMedico,
    string Crm,
    string UfCrm,
    string? Rqe,
    string TextoRodape,
    string? CarimboPngBase64 = null);

public sealed record PrepararResponse(string ToSignHashBase64, string AlgoritmoHash, string TransferStateBase64);

public sealed record ConcluirRequest(string TransferStateBase64, string RawSignatureBase64);

public sealed record ConcluirResponse(
    string PdfAssinadoBase64,
    string Formato,
    string? CertificadoTitular,
    string? CertificadoEmissor,
    string? CpfTitular);

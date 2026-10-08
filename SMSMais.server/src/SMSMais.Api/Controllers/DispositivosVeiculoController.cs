using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMais.Api.Auth;
using SMSMais.Core.Rastreamento.Dispositivos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Tablet fixo no veículo → Mapa da frota (docs/modulos/tfd/deslocamento-tablet.md).
/// Painel vincula/desvincula em <c>/veiculos/{id}/dispositivo</c>; o tablet ativa com o código e
/// depois fala com o próprio token no header <c>X-Dispositivo-Token</c>.
/// </summary>
[ApiController]
public sealed class DispositivosVeiculoController(IDispositivoVeiculoService service) : ControllerBase
{
    public const string PoliticaDeLimite = "tablet-veiculo";
    private const string CabecalhoToken = "X-Dispositivo-Token";

    // ---------------------------------------------------------------- painel (JWT, módulo Veículos)

    [HttpGet("veiculos/{veiculoId:guid}/dispositivo")]
    [RequerPermissao(ModuloPermissao.Veiculos, AcoesPermissao.Consulta)]
    public async Task<IActionResult> Obter(Guid veiculoId, CancellationToken ct)
    {
        var d = await service.ObterDoVeiculoAsync(veiculoId, ct);
        return d is null ? NoContent() : Ok(d);
    }

    /// <summary>Gera o código de 8 caracteres (24 h) que o tablet digita em "Vincular veículo".</summary>
    [HttpPost("veiculos/{veiculoId:guid}/dispositivo/codigo")]
    [RequerPermissao(ModuloPermissao.Veiculos, AcoesPermissao.Edicao)]
    public Task<CodigoAtivacaoDto> GerarCodigo(Guid veiculoId, CancellationToken ct) =>
        service.GerarCodigoAsync(veiculoId, ct);

    [HttpDelete("veiculos/{veiculoId:guid}/dispositivo")]
    [RequerPermissao(ModuloPermissao.Veiculos, AcoesPermissao.Edicao)]
    public async Task<IActionResult> Desvincular(Guid veiculoId, CancellationToken ct)
    {
        await service.DesvincularAsync(veiculoId, ct);
        return NoContent();
    }

    // ----------------------------------------------------- tablet: [AllowAnonymous], token conferido aqui

    [HttpPost("rastreamento/dispositivos/ativar")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> Ativar([FromBody] AtivarDispositivoRequest request, CancellationToken ct)
    {
        var ativado = await service.AtivarAsync(request, ct);
        return ativado is null
            ? NotFound(new { mensagem = "Código inválido, vencido ou já usado. Gere outro no painel." })
            : Ok(ativado);
    }

    [HttpGet("rastreamento/dispositivos/eu")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> Eu(CancellationToken ct)
    {
        var d = await service.AutenticarAsync(Request.Headers[CabecalhoToken].ToString(), ct);
        return d is null ? Unauthorized() : Ok(await service.VeiculoDoDispositivoAsync(d, ct));
    }

    [HttpPost("rastreamento/dispositivos/pontos")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> Pontos([FromBody] EnviarPosicoesRequest request, CancellationToken ct)
    {
        var d = await service.AutenticarAsync(Request.Headers[CabecalhoToken].ToString(), ct);
        if (d is null) return Unauthorized();
        await service.RegistrarPosicoesAsync(d, request, ct);
        return NoContent();
    }
}

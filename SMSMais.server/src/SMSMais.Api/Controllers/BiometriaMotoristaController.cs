using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMais.Api.Auth;
using SMSMais.Core.Rastreamento.Biometria;
using SMSMais.Core.Rastreamento.Dispositivos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Login do motorista no tablet do veículo pelo rosto (ADR-0073). O tablet fala pelo token do veículo
/// (<c>X-Dispositivo-Token</c>, rotas <c>rastreamento/dispositivos/*</c>); o painel, pelo JWT.
/// </summary>
[ApiController]
public sealed class BiometriaMotoristaController(
    IDispositivoVeiculoService dispositivos,
    IBiometriaTabletService tablet,
    IBiometriaPainelService painel) : ControllerBase
{
    private const string CabecalhoToken = "X-Dispositivo-Token";

    // ----------------------------------------------------- tablet: [AllowAnonymous], token conferido aqui

    /// <summary>Quem está escalado hoje no veículo do tablet, e se já cadastrou o rosto.</summary>
    [HttpGet("rastreamento/dispositivos/motoristas-do-dia")]
    [AllowAnonymous]
    [EnableRateLimiting(DispositivosVeiculoController.PoliticaDeLimite)]
    public async Task<IActionResult> MotoristasDoDia(CancellationToken ct)
    {
        var d = await Tablet(ct);
        return d is null ? Unauthorized() : Ok(await tablet.EscaladosDoDiaAsync(d, ct));
    }

    [HttpGet("rastreamento/dispositivos/motoristas/{motoristaId:guid}/referencia-facial")]
    [AllowAnonymous]
    [EnableRateLimiting(DispositivosVeiculoController.PoliticaDeLimite)]
    public async Task<IActionResult> Referencia(Guid motoristaId, CancellationToken ct)
    {
        var d = await Tablet(ct);
        return d is null ? Unauthorized() : Ok(await tablet.ReferenciaAsync(d, motoristaId, ct));
    }

    /// <summary>Primeiro acesso, passo 1: CPF → código no WhatsApp do cadastro do motorista.</summary>
    [HttpPost("rastreamento/dispositivos/primeiro-acesso/codigo")]
    [AllowAnonymous]
    [EnableRateLimiting(DispositivosVeiculoController.PoliticaDeLimite)]
    public async Task<IActionResult> SolicitarCodigo(
        [FromBody] SolicitarCodigoMotoristaRequest request, CancellationToken ct)
    {
        var d = await Tablet(ct);
        return d is null ? Unauthorized() : Ok(await tablet.SolicitarCodigoAsync(d, request, ct));
    }

    /// <summary>Passo 2: código conferido → ticket para mandar a foto + termo de ciência.</summary>
    [HttpPost("rastreamento/dispositivos/primeiro-acesso/confirmar")]
    [AllowAnonymous]
    [EnableRateLimiting(DispositivosVeiculoController.PoliticaDeLimite)]
    public async Task<IActionResult> ConfirmarCodigo(
        [FromBody] ConfirmarCodigoMotoristaRequest request, CancellationToken ct)
    {
        var d = await Tablet(ct);
        return d is null ? Unauthorized() : Ok(await tablet.ConfirmarCodigoAsync(d, request, ct));
    }

    /// <summary>Passo 3: foto do rosto (depois da prova de vida) vira a referência do motorista.</summary>
    [HttpPost("rastreamento/dispositivos/primeiro-acesso/rosto")]
    [AllowAnonymous]
    [EnableRateLimiting(DispositivosVeiculoController.PoliticaDeLimite)]
    public async Task<IActionResult> CadastrarRosto([FromBody] CadastrarRostoRequest request, CancellationToken ct)
    {
        var d = await Tablet(ct);
        if (d is null) return Unauthorized();
        var cadastrada = await tablet.CadastrarRostoAsync(d, request, ct);
        return StatusCode(StatusCodes.Status201Created, cadastrada);
    }

    /// <summary>Cada conferência pelo rosto (login ou início de translado), aprovada ou não.</summary>
    [HttpPost("rastreamento/dispositivos/verificacoes-faciais")]
    [AllowAnonymous]
    [EnableRateLimiting(DispositivosVeiculoController.PoliticaDeLimite)]
    public async Task<IActionResult> RegistrarVerificacao(
        [FromBody] RegistrarVerificacaoFacialRequest request, CancellationToken ct)
    {
        var d = await Tablet(ct);
        if (d is null) return Unauthorized();
        var registrada = await tablet.RegistrarVerificacaoAsync(d, request, ct);
        return StatusCode(StatusCodes.Status201Created, registrada);
    }

    /// <summary>Entrada pelo código de uso único que a central gerou para este veículo.</summary>
    [HttpPost("rastreamento/dispositivos/liberacao")]
    [AllowAnonymous]
    [EnableRateLimiting(DispositivosVeiculoController.PoliticaDeLimite)]
    public async Task<IActionResult> UsarLiberacao([FromBody] UsarLiberacaoRequest request, CancellationToken ct)
    {
        var d = await Tablet(ct);
        return d is null ? Unauthorized() : Ok(await tablet.UsarLiberacaoAsync(d, request, ct));
    }

    // ---------------------------------------------------------------- painel (JWT)

    [HttpGet("motoristas/{motoristaId:guid}/biometria")]
    [RequerPermissao(ModuloPermissao.Motoristas, AcoesPermissao.Consulta)]
    public Task<BiometriaMotoristaDto> Situacao(Guid motoristaId, CancellationToken ct) =>
        painel.SituacaoAsync(motoristaId, ct);

    [HttpGet("motoristas/{motoristaId:guid}/biometria/foto")]
    [RequerPermissao(ModuloPermissao.Motoristas, AcoesPermissao.Consulta)]
    public Task<FotoBiometriaDto> FotoReferencia(Guid motoristaId, CancellationToken ct) =>
        painel.FotoReferenciaAsync(motoristaId, ct);

    /// <summary>Apaga o rosto cadastrado: o motorista refaz o primeiro acesso no tablet.</summary>
    [HttpDelete("motoristas/{motoristaId:guid}/biometria")]
    [RequerPermissao(ModuloPermissao.Motoristas, AcoesPermissao.Edicao)]
    public async Task<IActionResult> Apagar(Guid motoristaId, CancellationToken ct)
    {
        await painel.ApagarAsync(motoristaId, ct);
        return NoContent();
    }

    /// <summary>Código de 6 números (15 min) para o motorista entrar no tablet deste veículo.</summary>
    [HttpPost("veiculos/{veiculoId:guid}/liberacao-motorista")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Edicao)]
    public Task<LiberacaoGeradaDto> GerarLiberacao(
        Guid veiculoId, [FromBody] GerarLiberacaoRequest request, CancellationToken ct) =>
        painel.GerarLiberacaoAsync(veiculoId, request, ct);

    [HttpGet("rastreamento/verificacoes-faciais")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Consulta)]
    public Task<IReadOnlyList<VerificacaoFacialDto>> ListarVerificacoes(
        [FromQuery] Guid? veiculoId, [FromQuery] Guid? motoristaId, [FromQuery] DateOnly? dia,
        CancellationToken ct) =>
        painel.ListarVerificacoesAsync(new FiltroVerificacoesFaciais(veiculoId, motoristaId, dia), ct);

    [HttpGet("rastreamento/verificacoes-faciais/{id:guid}/selfie")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Consulta)]
    public Task<FotoBiometriaDto> Selfie(Guid id, CancellationToken ct) => painel.SelfieAsync(id, ct);

    private Task<Data.Entities.DispositivoVeiculo?> Tablet(CancellationToken ct) =>
        dispositivos.AutenticarAsync(Request.Headers[CabecalhoToken].ToString(), ct);
}

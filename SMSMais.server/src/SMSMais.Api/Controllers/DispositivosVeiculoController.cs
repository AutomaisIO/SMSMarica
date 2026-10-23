using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMais.Api.Auth;
using SMSMais.Core.Rastreamento.Dispositivos;
using SMSMais.Core.Rastreamento.Ocorrencias;
using SMSMais.Core.Translado.Resgate;
using SMSMais.Core.Translado.Viagem;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Tablet fixo no veículo → Mapa da frota (docs/modulos/tfd/deslocamento-tablet.md).
/// Painel vincula/desvincula em <c>/veiculos/{id}/dispositivo</c>; o tablet ativa com o código e
/// depois fala com o próprio token no header <c>X-Dispositivo-Token</c>.
/// </summary>
[ApiController]
public sealed class DispositivosVeiculoController(
    IDispositivoVeiculoService service,
    IOcorrenciaVeiculoService ocorrencias,
    IRotaTabletService rotasTablet,
    IViagemService viagem,
    IResgateService resgates,
    SMSMais.Core.Translado.Localizacao.ICompartilhamentoLocalizacaoService localizacao) : ControllerBase
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

    /// <summary>
    /// Rotas de hoje do veículo do tablet, prontas para a navegação: paradas na ordem (coleta na casa
    /// de cada paciente, destino em cada unidade de atendimento), ponto no mapa, passageiros com os
    /// acompanhantes e assentos, condição do paciente e tempo médio do tratamento.
    /// </summary>
    [HttpGet("rastreamento/dispositivos/rotas-do-dia")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> RotasDoDia(CancellationToken ct)
    {
        var d = await service.AutenticarAsync(Request.Headers[CabecalhoToken].ToString(), ct);
        return d is null ? Unauthorized() : Ok(await rotasTablet.RotasDoDiaAsync(d, ct));
    }

    /// <summary>
    /// Botão REPORT do tablet (acidente, pane, paciente passou mal, outro). Idempotente pelo
    /// <c>idLocal</c>: o reenvio da fila offline devolve a mesma ocorrência. Alerta o painel na hora.
    /// </summary>
    [HttpPost("rastreamento/dispositivos/ocorrencias")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> Ocorrencia([FromBody] RegistrarOcorrenciaRequest request, CancellationToken ct)
    {
        var d = await service.AutenticarAsync(Request.Headers[CabecalhoToken].ToString(), ct);
        if (d is null) return Unauthorized();
        var registrada = await ocorrencias.RegistrarAsync(d, request, ct);
        return StatusCode(StatusCodes.Status201Created, registrada);
    }

    /// <summary>
    /// Marcação da viagem pelo tablet: início, embarque, ausência, desembarque e conclusão. Idempotente
    /// pelo <c>idLocal</c> (a fila sem rede reenvia). O desembarque do paciente conta o translado como
    /// realizado; a correção é no painel, e o que o painel corrigiu o tablet não muda mais.
    /// </summary>
    [HttpPost("rastreamento/dispositivos/viagem/eventos")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> EventoViagem([FromBody] RegistrarEventoViagemRequest request, CancellationToken ct)
    {
        var d = await service.AutenticarAsync(Request.Headers[CabecalhoToken].ToString(), ct);
        if (d is null) return Unauthorized();
        var registrado = await viagem.RegistrarDoTabletAsync(d, request, ct);
        return StatusCode(StatusCodes.Status201Created, registrado);
    }

    /// <summary>
    /// Os passageiros das rotas de hoje deste veículo que estão compartilhando a localização do celular
    /// enquanto esperam o embarque (a coleta na ida, o resgate marcado por este carro): a última posição,
    /// quando foi medida, a precisão e se o aparelho disse que é simulada (ADR-0082). O tablet consulta
    /// na tela de navegação e mantém a posição só em memória.
    /// </summary>
    [HttpGet("rastreamento/dispositivos/localizacao-passageiros")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> LocalizacaoPassageiros(CancellationToken ct)
    {
        var d = await service.AutenticarAsync(Request.Headers[CabecalhoToken].ToString(), ct);
        return d is null ? Unauthorized() : Ok(await localizacao.DoTabletAsync(d, ct));
    }

    /// <summary>
    /// "Pacientes disponíveis para resgate": a mesma lista para todos os tablets, com os lugares livres
    /// agora no carro desta rota, se cada um cabe e quem esta rota já marcou.
    /// </summary>
    [HttpGet("rastreamento/dispositivos/resgates")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> Resgates([FromQuery] Guid rotaId, CancellationToken ct)
    {
        var d = await service.AutenticarAsync(Request.Headers[CabecalhoToken].ToString(), ct);
        return d is null ? Unauthorized() : Ok(await resgates.ListarDoTabletAsync(d, rotaId, ct));
    }

    /// <summary>O motorista marca quem vai resgatar. 409 quando não cabe no carro agora ou outro carro
    /// marcou antes.</summary>
    [HttpPost("rastreamento/dispositivos/resgates")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> MarcarResgate([FromBody] MarcarResgateRequest request, CancellationToken ct)
    {
        var d = await service.AutenticarAsync(Request.Headers[CabecalhoToken].ToString(), ct);
        if (d is null) return Unauthorized();
        await resgates.MarcarDoTabletAsync(d, request, ct);
        return NoContent();
    }

    [HttpDelete("rastreamento/dispositivos/resgates/{sessaoId:guid}")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> DesmarcarResgate(Guid sessaoId, [FromQuery] Guid rotaId, CancellationToken ct)
    {
        var d = await service.AutenticarAsync(Request.Headers[CabecalhoToken].ToString(), ct);
        if (d is null) return Unauthorized();
        await resgates.DesmarcarDoTabletAsync(d, rotaId, sessaoId, ct);
        return NoContent();
    }

    /// <summary>O motorista que chegou ao local viu o paciente esperando: vale para todos os carros.</summary>
    [HttpPost("rastreamento/dispositivos/resgates/{sessaoId:guid}/no-local")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> ResgateNoLocal(Guid sessaoId, CancellationToken ct)
    {
        var d = await service.AutenticarAsync(Request.Headers[CabecalhoToken].ToString(), ct);
        if (d is null) return Unauthorized();
        await resgates.AvisarNoLocalAsync(sessaoId, CanalAvisoResgate.Motorista, ct);
        return NoContent();
    }
}

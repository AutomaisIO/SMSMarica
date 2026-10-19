using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Translado.Resgate;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Resgate pelo painel: a lista "Pacientes disponíveis para resgate" (a mesma dos tablets), a marcação de
/// quem uma rota vai buscar (só se couber no carro) e o aviso de que o paciente já está no local.
/// </summary>
[ApiController]
public sealed class ResgatesController(IResgateService service) : ControllerBase
{
    /// <summary>A lista de resgate de hoje. Com <c>rotaId</c>, também os lugares livres agora no carro da
    /// rota, se cada paciente cabe nele e quem a rota já marcou.</summary>
    [HttpGet("resgates")]
    [RequerPermissao(ModuloPermissao.Translados, AcoesPermissao.Consulta)]
    [ProducesResponseType<ResgatesDto>(StatusCodes.Status200OK)]
    public Task<ResgatesDto> Listar([FromQuery] Guid? rotaId, CancellationToken ct) =>
        service.ListarAsync(rotaId, ct);

    /// <summary>Marca quem a rota vai resgatar. 409 quando não cabe no carro agora ou outro carro já marcou.</summary>
    [HttpPost("rotas/{rotaId:guid}/resgates")]
    [RequerPermissao(ModuloPermissao.Translados, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Marcar(Guid rotaId, [FromBody] MarcarResgatesDaRotaRequest request, CancellationToken ct)
    {
        await service.MarcarAsync(rotaId, request.SessaoIds ?? [], ct);
        return NoContent();
    }

    /// <summary>Tira o paciente do resgate da rota (antes de embarcar): ele volta para a lista.</summary>
    [HttpDelete("rotas/{rotaId:guid}/resgates/{sessaoId:guid}")]
    [RequerPermissao(ModuloPermissao.Translados, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Desmarcar(Guid rotaId, Guid sessaoId, CancellationToken ct)
    {
        await service.DesmarcarAsync(rotaId, sessaoId, ct);
        return NoContent();
    }

    /// <summary>A central avisa que o paciente já está no local esperando (ligação da clínica ou dele).</summary>
    [HttpPost("resgates/{sessaoId:guid}/no-local")]
    [RequerPermissao(ModuloPermissao.Translados, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AvisarNoLocal(Guid sessaoId, CancellationToken ct)
    {
        await service.AvisarNoLocalAsync(sessaoId, CanalAvisoResgate.Central, ct);
        return NoContent();
    }
}

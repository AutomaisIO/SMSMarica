using Automais.Pabx.Api.Asterisk;
using Automais.Pabx.Api.Ramais;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Pabx.Api.Controllers;

[ApiController]
[Route("api/ramais")]
public sealed class RamaisController(
    IRamalService ramalService,
    IStatusService statusService,
    IGeradorConfigSip geradorConfig) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<RamalDto>> Listar([FromQuery] int? unidadeId, CancellationToken ct) =>
        ramalService.ListarAsync(unidadeId, ct);

    [HttpGet("status")]
    public Task<StatusGeralDto> Status(CancellationToken ct) =>
        statusService.ObterStatusAsync(ct);

    [HttpGet("{numero}")]
    public Task<RamalDto> Obter(string numero, CancellationToken ct) =>
        ramalService.ObterAsync(numero, ct);

    [HttpPost]
    public async Task<ActionResult<RamalComSecretDto>> Criar([FromBody] CriarRamalRequest request, CancellationToken ct)
    {
        var criado = await ramalService.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { numero = criado.Ramal.Numero }, criado);
    }

    [HttpPut("{numero}")]
    public Task<RamalDto> Atualizar(string numero, [FromBody] AtualizarRamalRequest request, CancellationToken ct) =>
        ramalService.AtualizarAsync(numero, request, ct);

    [HttpDelete("{numero}")]
    public async Task<IActionResult> Excluir(string numero, CancellationToken ct)
    {
        await ramalService.ExcluirAsync(numero, ct);
        return NoContent();
    }

    [HttpPost("{numero}/reset-secret")]
    public Task<RamalComSecretDto> ResetSecret(string numero, CancellationToken ct) =>
        ramalService.ResetSecretAsync(numero, ct);

    [HttpPost("adotar")]
    public Task<AdocaoResultadoDto> Adotar([FromBody] AdotarRamaisRequest request, CancellationToken ct) =>
        ramalService.AdotarAsync(request, ct);

    /// <summary>Regenera o sip_smsmarica.conf e recarrega o SIP — para reaplicar sem mudar nada.</summary>
    [HttpPost("aplicar")]
    public Task<AplicacaoResultado> Aplicar(CancellationToken ct) =>
        geradorConfig.AplicarAsync(ct);
}

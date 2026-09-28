using Automais.Pabx.Api.Asterisk;
using Automais.Pabx.Api.Comandos;
using Automais.Pabx.Api.Data.Entities;
using Automais.Pabx.Api.Ramais;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Pabx.Api.Controllers;

[ApiController]
[Route("api/ramais")]
public sealed class RamaisController(
    IRamalService ramalService,
    IStatusService statusService,
    IComandosAsterisk comandos,
    IGeradorConfigSip geradorConfig) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<RamalDto>> Listar(
        [FromQuery] int? unidadeId,
        [FromQuery] TipoRamal? tipo,
        [FromQuery] string? donoSistema,
        [FromQuery] string? donoId,
        CancellationToken ct) =>
        ramalService.ListarAsync(new FiltroRamais(unidadeId, tipo, donoSistema, donoId), ct);

    [HttpGet("status")]
    public Task<StatusGeralDto> Status(CancellationToken ct) =>
        statusService.ObterStatusAsync(ct);

    /// <summary>Próximos números livres da faixa do tipo (inventário + sip_custom.conf legado).</summary>
    [HttpGet("faixas/livres")]
    public Task<FaixaLivreDto> Livres([FromQuery] TipoRamal tipo = TipoRamal.Fisico, [FromQuery] int quantidade = 10, CancellationToken ct = default) =>
        ramalService.SugerirLivresAsync(tipo, quantidade, ct);

    [HttpGet("{numero}")]
    public Task<RamalDto> Obter(string numero, CancellationToken ct) =>
        ramalService.ObterAsync(numero, ct);

    [HttpPost]
    public async Task<ActionResult<RamalComSecretDto>> Criar([FromBody] CriarRamalRequest request, CancellationToken ct)
    {
        var criado = await ramalService.CriarAsync(request, ct);
        SemCache();
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
    public async Task<RamalComSecretDto> ResetSecret(string numero, CancellationToken ct)
    {
        var resultado = await ramalService.ResetSecretAsync(numero, ct);
        SemCache();
        return resultado;
    }

    /// <summary>Contexto do dialplan, codecs e limite de chamadas simultâneas.</summary>
    [HttpGet("{numero}/config")]
    public Task<ConfigRamalDto> ObterConfig(string numero, CancellationToken ct) =>
        ramalService.ObterConfigAsync(numero, ct);

    [HttpPut("{numero}/config")]
    public Task<ConfigRamalDto> AtualizarConfig(string numero, [FromBody] AtualizarConfigRamalRequest request, CancellationToken ct) =>
        ramalService.AtualizarConfigAsync(numero, request, ct);

    /// <summary>Vincula o ramal a um usuário de sistema externo (ex.: SMSMais); Sistema e Id nulos desvinculam.</summary>
    [HttpPut("{numero}/dono")]
    public Task<RamalDto> DefinirDono(string numero, [FromBody] DefinirDonoRequest request, CancellationToken ct) =>
        ramalService.DefinirDonoAsync(numero, request, ct);

    /// <summary>
    /// Credencial SIP do softphone (contém a senha em claro). Para o sistema dono repassar ao
    /// usuário logado — nunca cacheada, nunca logada.
    /// </summary>
    [HttpGet("{numero}/credencial")]
    public async Task<CredencialSipDto> Credencial(string numero, CancellationToken ct)
    {
        var credencial = await ramalService.ObterCredencialAsync(numero, ct);
        SemCache();
        return credencial;
    }

    /// <summary>Estado do ramal no Asterisk agora (AMI SIPshowpeer).</summary>
    [HttpGet("{numero}/peer")]
    public Task<PeerDetalheDto> Peer(string numero, CancellationToken ct) =>
        comandos.DetalharPeerAsync(numero, ct);

    [HttpPost("adotar")]
    public Task<AdocaoResultadoDto> Adotar([FromBody] AdotarRamaisRequest request, CancellationToken ct) =>
        ramalService.AdotarAsync(request, ct);

    /// <summary>Regenera o sip_smsmarica.conf e recarrega o SIP — para reaplicar sem mudar nada.</summary>
    [HttpPost("aplicar")]
    public Task<AplicacaoResultado> Aplicar(CancellationToken ct) =>
        geradorConfig.AplicarAsync(ct);

    private void SemCache()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }
}

using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.ElevenLabs;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>Configuração da integração ElevenLabs (fala-para-texto do Agente IA).</summary>
[ApiController]
[Route("integracoes/elevenlabs")]
public sealed class ElevenLabsConfigController(
    IElevenLabsConfigService service, IElevenLabsTtsService tts) : ControllerBase
{
    private readonly IElevenLabsConfigService _service = service;
    private readonly IElevenLabsTtsService _tts = tts;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Consulta)]
    [ProducesResponseType<ElevenLabsConfigDto>(StatusCodes.Status200OK)]
    public async Task<ElevenLabsConfigDto> Obter(CancellationToken cancellationToken) =>
        await _service.ObterAsync(cancellationToken);

    /// <summary>Vozes da conta ElevenLabs, para o seletor da tela. Vazio se sem chave/acesso.</summary>
    [HttpGet("vozes")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<VozElevenLabs>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<VozElevenLabs>> Vozes(CancellationToken cancellationToken) =>
        await _tts.ListarVozesAsync(cancellationToken);

    [HttpPut]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Atualizar(
        [FromBody] AtualizarElevenLabsConfigRequest request, CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Busca vozes pt-BR na biblioteca pública, com filtros de gênero e idade.</summary>
    [HttpGet("biblioteca")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<VozBibliotecaElevenLabs>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<VozBibliotecaElevenLabs>> Biblioteca(
        [FromQuery] string? genero, [FromQuery] string? idade, [FromQuery] string? busca,
        CancellationToken cancellationToken) =>
        await _tts.ListarBibliotecaPtBrAsync(genero, idade, busca, cancellationToken);

    /// <summary>Adiciona uma voz da biblioteca à conta e passa a usá-la nas respostas em áudio.</summary>
    [HttpPost("biblioteca/usar")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UsarVoz(
        [FromBody] UsarVozBibliotecaRequest request, CancellationToken cancellationToken)
    {
        var vozId = await _tts.AdicionarVozAsync(request.PublicOwnerId, request.VozId, request.Nome, cancellationToken);
        if (vozId is null)
            return UnprocessableEntity(new { erro = "Não foi possível adicionar a voz (verifique a chave e o limite de vozes da conta)." });
        await _service.DefinirVozAsync(vozId, cancellationToken);
        return Ok(new { vozId });
    }
}

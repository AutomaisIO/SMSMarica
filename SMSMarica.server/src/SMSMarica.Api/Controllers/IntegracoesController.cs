using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Integracoes;
using SMSMarica.Core.Integracoes.Dtos;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Proxy para integrações externas (Hub do Desenvolvedor). Mantém o token
/// no servidor e permite caching/auditoria centralizados.
/// </summary>
[ApiController]
[Route("integracoes")]
public sealed class IntegracoesController(IHubConsultaService hub) : ControllerBase
{
    private readonly IHubConsultaService _hub = hub;

    /// <summary>Consulta CPF na Receita exigindo data de nascimento.</summary>
    [HttpGet("cpf")]
    [ProducesResponseType<HubCpfRespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<HubCpfRespostaDto> ConsultarCpf(
        [FromQuery] string cpf,
        [FromQuery] DateOnly dataNascimento,
        CancellationToken cancellationToken) =>
        await _hub.ConsultarCpfAsync(cpf, dataNascimento, cancellationToken);

    /// <summary>Consulta endereço por CEP.</summary>
    [HttpGet("cep/{cep}")]
    [ProducesResponseType<HubCepRespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<HubCepRespostaDto> ConsultarCep(
        string cep,
        CancellationToken cancellationToken) =>
        await _hub.ConsultarCepAsync(cep, cancellationToken);
}

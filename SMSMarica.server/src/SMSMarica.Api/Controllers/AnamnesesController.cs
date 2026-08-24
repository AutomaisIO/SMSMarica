using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Anamneses;
using SMSMarica.Core.Anamneses.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Anamnese (questionário pré-exame) vinculada à solicitação de exame.
/// Preenchida/reaberta pela enfermagem ou atendimento; consultada pelo médico
/// ao laudar. Conteúdo versionado por tipo de questionário (hoje: mamografia v1).
/// </summary>
[ApiController]
[Route("anamneses")]
public sealed class AnamnesesController(IAnamnesesService service) : ControllerBase
{
    /// <summary>
    /// Contexto da tela de anamnese (pedido + paciente + anamnese existente).
    /// Informar <c>solicitacaoExameId</c> OU <c>accessionNumber</c>.
    /// </summary>
    [HttpGet("contexto")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<AnamneseContextoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AnamneseContextoDto> ObterContexto(
        [FromQuery] Guid? solicitacaoExameId,
        [FromQuery] string? accessionNumber,
        CancellationToken cancellationToken) =>
        await service.ObterContextoAsync(solicitacaoExameId, accessionNumber, cancellationToken);

    /// <summary>Cria ou atualiza (upsert) a anamnese da solicitação — pode ser reaberta e editada.</summary>
    [HttpPut("{solicitacaoExameId:guid}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType<AnamneseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AnamneseDto> Salvar(
        Guid solicitacaoExameId,
        [FromBody] SalvarAnamneseDto dto,
        CancellationToken cancellationToken) =>
        await service.SalvarAsync(solicitacaoExameId, dto, cancellationToken);
}

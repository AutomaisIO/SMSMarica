using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Consultas;
using SMSMarica.Core.Consultas.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Solicitações de CONSULTA importadas do SISREG (categoria não-imagem). Sem PACS/laudo — o
/// exame de imagem tem seu próprio módulo. Ver ADR-0021.
/// </summary>
[ApiController]
[Route("consultas")]
public sealed class ConsultasController(IConsultasService service) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Consultas, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ConsultaListItemDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ConsultaListItemDto>> Listar(
        [FromQuery] Guid? pacienteId,
        [FromQuery] string? busca,
        [FromQuery] DateOnly? dataInicial,
        [FromQuery] DateOnly? dataFinal,
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default)
        => service.ListarAsync(new FiltroConsultasDto(pacienteId, busca, dataInicial, dataFinal, limite), cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Consultas, AcoesPermissao.Consulta)]
    [ProducesResponseType<ConsultaDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsultaDetalheDto>> Obter(Guid id, CancellationToken cancellationToken)
        => await service.ObterPorIdAsync(id, cancellationToken) is { } dto ? Ok(dto) : NotFound();
}

using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Solicitações de exame que geram worklist DICOM no dcm4chee. Ciclo:
/// Solicitada → Agendada (UPS-RS) → EmExecucao → Realizada → Laudada.
/// </summary>
[ApiController]
[Route("solicitacoes-exame")]
public sealed class SolicitacoesExameController(ISolicitacoesExameService service) : ControllerBase
{
    private readonly ISolicitacoesExameService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SolicitacaoExameListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<SolicitacaoExameListItemDto>> Listar(
        [FromQuery] StatusSolicitacaoExame? status,
        [FromQuery] Guid? pacienteId,
        [FromQuery] Guid? unidadeId,
        [FromQuery] Guid? tipoExameId,
        [FromQuery] DateOnly? dataInicial,
        [FromQuery] DateOnly? dataFinal,
        [FromQuery] string? accessionNumber,
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default) =>
        await _service.ListarAsync(
            new FiltroSolicitacoesDto(status, pacienteId, unidadeId, tipoExameId, dataInicial, dataFinal, accessionNumber, limite),
            cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<SolicitacaoExameDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<SolicitacaoExameDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpGet("por-accession/{accession}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<SolicitacaoExameDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ObterPorAccession(string accession, CancellationToken cancellationToken)
    {
        var dto = await _service.ObterPorAccessionAsync(accession, cancellationToken);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpGet("por-study/{studyInstanceUID}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<SolicitacaoExameDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ObterPorStudy(string studyInstanceUID, CancellationToken cancellationToken)
    {
        var dto = await _service.ObterPorStudyAsync(studyInstanceUID, cancellationToken);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpPost]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarSolicitacaoExameRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarSolicitacaoExameRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancelar")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(
        Guid id,
        [FromBody] CancelarSolicitacaoExameRequest request,
        CancellationToken cancellationToken)
    {
        await _service.CancelarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reenviar-worklist")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReenviarWorklist(Guid id, CancellationToken cancellationToken)
    {
        await _service.ReenviarWorklistAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        await _service.ExcluirAsync(id, cancellationToken);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.EscopoExames;
using SMSMais.Core.EscopoExames.Dtos;
using SMSMais.Core.Worklist;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// O que cada unidade executa de imagem: worklist e aparelho de destino por par (tipo, unidade).
/// Substitui a flag global <c>TipoExame.EnviarParaWorklist</c>, que não conseguia valer ao mesmo
/// tempo para quem tem aparelho e para quem não tem.
/// </summary>
[ApiController]
[Route("escopo-exames")]
public sealed class EscopoExamesController(
    IEscopoExamesService service,
    IBackfillEscopoExameUnidade backfill) : ControllerBase
{
    /// <summary>Os exames de imagem que esta unidade executa — a aba "Exames de imagem".</summary>
    [HttpGet("unidade/{unidadeId:guid}")]
    [RequerPermissao(ModuloPermissao.TiposExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<EscopoExameItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<EscopoExameItemDto>> ListarDaUnidade(
        Guid unidadeId,
        [FromQuery] bool incluirInativos,
        CancellationToken cancellationToken) =>
        await service.ListarDaUnidadeAsync(unidadeId, incluirInativos, cancellationToken);

    /// <summary>Em quais unidades este exame está no escopo — a aba "Unidades que executam".</summary>
    [HttpGet("tipo/{tipoExameId:guid}")]
    [RequerPermissao(ModuloPermissao.TiposExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<EscopoExameItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<EscopoExameItemDto>> ListarDoTipo(
        Guid tipoExameId, CancellationToken cancellationToken) =>
        await service.ListarDoTipoAsync(tipoExameId, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.TiposExame, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Adicionar(
        [FromBody] AdicionarEscopoExameRequest request, CancellationToken cancellationToken)
    {
        var id = await service.AdicionarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListarDaUnidade), new { unidadeId = request.UnidadeId }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.TiposExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id, [FromBody] AtualizarEscopoExameRequest request, CancellationToken cancellationToken)
    {
        await service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Tira o exame do escopo da unidade (soft delete). Não apaga exame nenhum.</summary>
    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.TiposExame, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        await service.RemoverAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Popula o escopo a partir do histórico de exames, herdando a flag do tipo — o que faz o
    /// comportamento no cutover ser idêntico ao de antes. Idempotente: rodar de novo não duplica.
    /// Use <c>simular=true</c> para ver o que aconteceria sem gravar.
    /// </summary>
    [HttpPost("backfill")]
    [RequerPermissao(ModuloPermissao.TiposExame, AcoesPermissao.Edicao)]
    [ProducesResponseType<ResultadoBackfillEscopo>(StatusCodes.Status200OK)]
    public async Task<ResultadoBackfillEscopo> Backfill(
        [FromQuery] bool simular, CancellationToken cancellationToken) =>
        await backfill.ExecutarAsync(simular, cancellationToken);
}

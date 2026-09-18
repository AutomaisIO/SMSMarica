using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.EstrategiasFila;
using SMSMais.Core.EstrategiasFila.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Estratégias de fila (ADR-0058): simular mudanças na oferta de um procedimento contra a fila
/// real, pedir ao agente uma estratégia, salvar, reabrir, rerodar e comparar.
///
/// <para><b>Só planejamento.</b> Nenhum endpoint aqui escreve no SISREG nem em sistema externo. A
/// estratégia fica no nosso banco para consulta; "marcar como aplicada" é anotação humana.</para>
///
/// <para>Escopo: rede toda — é planejamento, o mesmo critério da Demanda regulada.</para>
/// </summary>
[ApiController]
[Route("agenda/estrategias")]
public sealed class EstrategiasFilaController(
    ICenarioFilaService cenarios,
    IEstrategiaFilaService estrategias) : ControllerBase
{
    /// <summary>A lista de procedimentos com o tamanho da fila ao lado do nome.</summary>
    [HttpGet("procedimentos")]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ProcedimentoComFilaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ProcedimentoComFilaDto>> Procedimentos(
        [FromQuery] string? busca, [FromQuery] string? ordenar, CancellationToken ct) =>
        await cenarios.ListarProcedimentosAsync(busca, ordenar, ct);

    /// <summary>Cenário atual do procedimento + projeção com o que existe hoje.</summary>
    [HttpGet("cenario")]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Consulta)]
    [ProducesResponseType<SimularRespostaDto>(StatusCodes.Status200OK)]
    public async Task<SimularRespostaDto> Cenario(
        [FromQuery] string? procedimentoCodigo, [FromQuery] string procedimentoNome, CancellationToken ct) =>
        await estrategias.SimularAsync(new SimularRequest(procedimentoCodigo, procedimentoNome, null!), ct);

    /// <summary>Projeção com parâmetros do operador. Não grava.</summary>
    [HttpPost("simular")]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Consulta)]
    [ProducesResponseType<SimularRespostaDto>(StatusCodes.Status200OK)]
    public async Task<SimularRespostaDto> Simular([FromBody] SimularRequest request, CancellationToken ct) =>
        await estrategias.SimularAsync(request, ct);

    [HttpGet]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<EstrategiaResumoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<EstrategiaResumoDto>> Listar(
        [FromQuery] StatusEstrategiaFila? status,
        [FromQuery] string? procedimentoCodigo,
        [FromQuery] string? procedimentoNome,
        [FromQuery] bool incluirArquivadas,
        CancellationToken ct) =>
        await estrategias.ListarAsync(new EstrategiaFiltro(status, procedimentoCodigo, procedimentoNome, incluirArquivadas), ct);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Consulta)]
    [ProducesResponseType<EstrategiaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<EstrategiaDto> Obter(Guid id, CancellationToken ct) =>
        await estrategias.ObterAsync(id, ct);

    [HttpGet("{id:guid}/rodadas/{numero:int}")]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Consulta)]
    [ProducesResponseType<RodadaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RodadaDto> ObterRodada(Guid id, int numero, CancellationToken ct) =>
        await estrategias.ObterRodadaAsync(id, numero, ct);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarEstrategiaRequest request, CancellationToken ct)
    {
        var id = await estrategias.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarEstrategiaRequest request, CancellationToken ct)
    {
        await estrategias.AtualizarAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Nova rodada. Modo agente chama a IA (30–90 s) — o front mostra progresso.</summary>
    [HttpPost("{id:guid}/rodadas")]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Edicao)]
    [ProducesResponseType<RodadaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<RodadaDto> Rodar(Guid id, [FromBody] NovaRodadaRequest request, CancellationToken ct) =>
        await estrategias.RodarAsync(id, request, ct);

    [HttpPost("{id:guid}/marcar-aplicada")]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarAplicada(Guid id, [FromBody] MarcarAplicadaRequest request, CancellationToken ct)
    {
        await estrategias.MarcarAplicadaAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/arquivar")]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Arquivar(Guid id, CancellationToken ct)
    {
        await estrategias.ArquivarAsync(id, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.EstrategiasFila, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await estrategias.ExcluirAsync(id, ct);
        return NoContent();
    }
}

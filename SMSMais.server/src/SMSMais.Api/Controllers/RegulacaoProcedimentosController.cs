using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Core.Regulacao.Catalogo.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Catálogo canônico de procedimentos da Regulação (ADR-0052) — a busca que abre o wizard de
/// solicitação, mais a curadoria do pareamento entre sistemas.
///
/// <para><b>Lê o NOSSO catálogo, não os sistemas de regulação.</b> Quem fala com o SISREG, o SER
/// e o SERNIT são as varreduras e os syncs de catálogo, em background; aqui só se lê o espelho
/// já consolidado. Nenhum endpoint deste controller escreve em sistema externo.</para>
///
/// <para>Duas permissões: <see cref="ModuloPermissao.Regulacao"/> (47) para quem abre solicitação
/// e só precisa buscar; <see cref="ModuloPermissao.RegulacaoConfiguracao"/> (51) para a curadoria,
/// que muda o catálogo para todo mundo.</para>
/// </summary>
[ApiController]
[Route("regulacao/procedimentos")]
public sealed class RegulacaoProcedimentosController(
    IRegulacaoProcedimentoBuscaService busca,
    IRegulacaoCatalogoService catalogo) : ControllerBase
{
    /// <summary>
    /// Busca por texto livre: lexical (nome canônico e rótulo de cada origem) somada à semântica.
    /// Devolve a oferta interna e a existência externa junto, para a tela não precisar de N idas.
    /// </summary>
    [HttpGet("buscar")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<RegulacaoBuscaResultadoDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoBuscaResultadoDto> Buscar(
        [FromQuery] string q,
        [FromQuery] TipoProcedimentoRegulacao? tipo,
        [FromQuery] int limite,
        CancellationToken cancellationToken) =>
        busca.BuscarAsync(q, tipo, limite, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<RegulacaoProcedimentoDetalheDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoProcedimentoDetalheDto> Obter(Guid id, CancellationToken cancellationToken) =>
        busca.ObterAsync(id, cancellationToken);

    /// <summary>
    /// Relê as três origens e recalcula embeddings e sugestões. Idempotente — roda sozinho ao fim
    /// dos syncs de catálogo; este endpoint é o "agora" da tela de curadoria.
    /// </summary>
    [HttpPost("sincronizar")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegulacaoCatalogoSyncResultadoDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoCatalogoSyncResultadoDto> Sincronizar(CancellationToken cancellationToken) =>
        catalogo.SincronizarAsync(cancellationToken);

    /// <summary>Pares que o robô propôs e que esperam confirmação humana.</summary>
    [HttpGet("sugestoes")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<RegulacaoSugestaoPareamentoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<RegulacaoSugestaoPareamentoDto>> Sugestoes(CancellationToken cancellationToken) =>
        catalogo.ListarSugestoesAsync(cancellationToken);

    [HttpPost("origens/{origemId:guid}/confirmar")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ConfirmarPareamento(
        Guid origemId, [FromBody] ConfirmarPareamentoRequest req, CancellationToken cancellationToken)
    {
        await catalogo.ConfirmarPareamentoAsync(origemId, req.ProcedimentoId, cancellationToken);
        return NoContent();
    }

    [HttpPost("origens/{origemId:guid}/rejeitar")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RejeitarPareamento(Guid origemId, CancellationToken cancellationToken)
    {
        await catalogo.RejeitarPareamentoAsync(origemId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/nome")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Renomear(
        Guid id, [FromBody] RenomearCanonicoRequest req, CancellationToken cancellationToken)
    {
        await catalogo.RenomearCanonicoAsync(id, req.Nome, cancellationToken);
        return NoContent();
    }

    public sealed record ConfirmarPareamentoRequest(Guid ProcedimentoId);

    public sealed record RenomearCanonicoRequest(string Nome);
}

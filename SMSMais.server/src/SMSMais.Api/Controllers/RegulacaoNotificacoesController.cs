using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Notificacoes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// O que mudou nas solicitações e ainda não foi visto (plano 05).
///
/// <para>Não há tabela de avisos: a notificação é uma leitura da própria trilha, filtrada pelos
/// eventos que pedem atenção. O "visto" é por usuário — a mesma movimentação interessa a quem
/// abriu o pedido e ao agente, e um não apaga o aviso do outro.</para>
/// </summary>
[ApiController]
[Route("regulacao/notificacoes")]
public sealed class RegulacaoNotificacoesController(IRegulacaoNotificacaoService servico) : ControllerBase
{
    /// <param name="escopo"><c>minha</c> (default) ou <c>todas</c> — esta exige o módulo 48 ou a configuração aberta.</param>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaNotificacoesRegulacaoDto>(StatusCodes.Status200OK)]
    public Task<PaginaNotificacoesRegulacaoDto> Listar(
        [FromQuery] RegulacaoNotificacaoFiltro filtro, CancellationToken cancellationToken) =>
        servico.ListarAsync(filtro, cancellationToken);

    /// <summary>Quantas não vistas — é o número do badge.</summary>
    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<RegulacaoNotificacaoResumoDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoNotificacaoResumoDto> Resumo(
        [FromQuery] string escopo = "minha", CancellationToken cancellationToken = default) =>
        servico.ResumoAsync(escopo, cancellationToken);

    [HttpPost("{eventoId:guid}/vista")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarVista(Guid eventoId, CancellationToken cancellationToken)
    {
        await servico.MarcarVistaAsync(eventoId, cancellationToken);
        return NoContent();
    }

    /// <summary>Abrir o detalhe da solicitação zera os avisos dela.</summary>
    [HttpPost("solicitacao/{solicitacaoId:guid}/vistas")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarVistasDaSolicitacao(
        Guid solicitacaoId, CancellationToken cancellationToken)
    {
        await servico.MarcarVistasDaSolicitacaoAsync(solicitacaoId, cancellationToken);
        return NoContent();
    }
}

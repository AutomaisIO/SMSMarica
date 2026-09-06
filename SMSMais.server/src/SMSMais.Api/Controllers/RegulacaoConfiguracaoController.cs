using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Configuracao.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Configuração do módulo Regulação → Solicitações (ADR-0052).
///
/// <para>Duas leituras, de propósito: a completa exige o módulo de configuração (51); a de
/// <c>fluxo</c> serve o wizard e basta ter o módulo do solicitante (47). Os cortes da busca, o
/// prazo do SISREG e as regras de follow-up são parâmetros de operação e não trafegam para a
/// ponta.</para>
/// </summary>
[ApiController]
[Route("regulacao/configuracao")]
public sealed class RegulacaoConfiguracaoController(IRegulacaoConfiguracaoService servico) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<RegulacaoConfiguracaoDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoConfiguracaoDto> Obter(CancellationToken cancellationToken) =>
        servico.ObterAsync(cancellationToken);

    /// <summary>O que o wizard precisa saber para se comportar. Qualquer solicitante enxerga.</summary>
    [HttpGet("fluxo")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<RegulacaoConfiguracaoFluxoDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoConfiguracaoFluxoDto> Fluxo(CancellationToken cancellationToken) =>
        servico.ObterFluxoAsync(cancellationToken);

    /// <summary>`RowVersion` divergente devolve 409 — alguém salvou antes.</summary>
    [HttpPut]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegulacaoConfiguracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<RegulacaoConfiguracaoDto> Atualizar(
        [FromBody] AtualizarRegulacaoConfiguracaoRequest req, CancellationToken cancellationToken) =>
        servico.AtualizarAsync(req, cancellationToken);
}

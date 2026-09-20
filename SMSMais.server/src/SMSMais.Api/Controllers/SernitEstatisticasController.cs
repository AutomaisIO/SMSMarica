using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Estatisticas;
using SMSMais.Core.Regulacao.Estatisticas.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// SERNIT → <b>Estatísticas</b> do trabalho dos operadores (equipe e individual), a partir da trilha
/// de eventos da fila espelhada. Somente leitura sobre o nosso banco — nada aqui fala com o SERNIT.
///
/// <para>Ver: <see cref="ModuloPermissao.EstatisticaSernit"/>. Escolher quem entra nas estatísticas:
/// <see cref="ModuloPermissao.RegulacaoConfiguracao"/> — muda o que todos veem.</para>
/// </summary>
[ApiController]
[Route("regulacao/sernit/estatisticas/operadores")]
public sealed class SernitEstatisticasController(IEstatisticasOperadoresExternosService estatisticas) : ControllerBase
{
    private const FonteEstatisticaExterna Fonte = FonteEstatisticaExterna.Sernit;

    /// <summary>A equipe (nomes habilitados) no período, com o período anterior de mesmo tamanho.</summary>
    /// <param name="de">Primeiro dia (data do evento, em Brasília).</param>
    /// <param name="ate">Último dia, inclusive. Até 366 dias.</param>
    [HttpGet("equipe")]
    [RequerPermissao(ModuloPermissao.EstatisticaSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<EquipeExternaEstatisticaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<EquipeExternaEstatisticaDto> Equipe(
        [FromQuery] DateOnly de, [FromQuery] DateOnly ate, CancellationToken cancellationToken = default) =>
        await estatisticas.EquipeAsync(Fonte, de, ate, cancellationToken);

    /// <summary>Uma pessoa no período.</summary>
    /// <param name="chave">A <c>chave</c> da linha do ranking (<c>o:&lt;NOME&gt;</c>).</param>
    [HttpGet("individual")]
    [RequerPermissao(ModuloPermissao.EstatisticaSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<IndividualExternoEstatisticaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IndividualExternoEstatisticaDto> Individual(
        [FromQuery] string chave, [FromQuery] DateOnly de, [FromQuery] DateOnly ate,
        CancellationToken cancellationToken = default) =>
        await estatisticas.IndividualAsync(Fonte, chave, de, ate, cancellationToken);

    /// <summary>Todos os nomes que já assinaram um evento, e quem está marcado.</summary>
    [HttpGet("configuracao")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<OperadoresExternosConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<OperadoresExternosConfiguracaoDto> Configuracao(CancellationToken cancellationToken = default) =>
        await estatisticas.ConfiguracaoAsync(Fonte, cancellationToken);

    /// <summary>Grava quem entra nas estatísticas (substitui a lista inteira).</summary>
    [HttpPut("configuracao")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<OperadoresExternosConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<OperadoresExternosConfiguracaoDto> SalvarConfiguracao(
        [FromBody] SalvarOperadoresExternosHabilitadosRequest request, CancellationToken cancellationToken = default) =>
        await estatisticas.SalvarHabilitadosAsync(Fonte, request.Nomes, cancellationToken);
}

using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.SisregWeb.Estatisticas;
using SMSMais.Core.Integracoes.SisregWeb.Estatisticas.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// SISREG → <b>Estatísticas</b> do trabalho dos operadores da regulação (equipe e individual), a
/// partir do export da agenda já importado. Somente leitura sobre o nosso banco — nada aqui fala
/// com o SISREG.
///
/// <para>Ver: <see cref="ModuloPermissao.EstatisticaSisreg"/> — módulo próprio desde 20/09/2026, e
/// não mais <see cref="ModuloPermissao.Sisreg"/>: quem consulta a agenda não precisa ver a produção
/// de cada colega. Escolher quem entra nas estatísticas: <see cref="ModuloPermissao.SisregConfiguracao"/>
/// — muda o que todos veem.</para>
/// </summary>
[ApiController]
[Route("sisreg/estatisticas/operadores")]
public sealed class SisregEstatisticasController(IEstatisticasOperadoresService estatisticas) : ControllerBase
{
    /// <summary>A equipe (logins habilitados) no período, com o período anterior de mesmo tamanho.</summary>
    /// <param name="de">Primeiro dia (data da autorização).</param>
    /// <param name="ate">Último dia, inclusive. Até 366 dias.</param>
    [HttpGet("equipe")]
    [RequerPermissao(ModuloPermissao.EstatisticaSisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<EquipeEstatisticaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<EquipeEstatisticaDto> Equipe(
        [FromQuery] DateOnly de, [FromQuery] DateOnly ate, CancellationToken cancellationToken = default) =>
        await estatisticas.EquipeAsync(de, ate, cancellationToken);

    /// <summary>Uma pessoa no período.</summary>
    /// <param name="chave">A <c>chave</c> da linha do ranking (<c>u:&lt;id&gt;</c> ou <c>l:&lt;LOGIN&gt;</c>).</param>
    [HttpGet("individual")]
    [RequerPermissao(ModuloPermissao.EstatisticaSisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<IndividualEstatisticaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IndividualEstatisticaDto> Individual(
        [FromQuery] string chave, [FromQuery] DateOnly de, [FromQuery] DateOnly ate,
        CancellationToken cancellationToken = default) =>
        await estatisticas.IndividualAsync(chave, de, ate, cancellationToken);

    /// <summary>Todos os logins que já autorizaram algo, e quem está marcado.</summary>
    [HttpGet("configuracao")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<OperadoresConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<OperadoresConfiguracaoDto> Configuracao(CancellationToken cancellationToken = default) =>
        await estatisticas.ConfiguracaoAsync(cancellationToken);

    /// <summary>Grava quem entra nas estatísticas (substitui a lista inteira).</summary>
    [HttpPut("configuracao")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<OperadoresConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<OperadoresConfiguracaoDto> SalvarConfiguracao(
        [FromBody] SalvarOperadoresHabilitadosRequest request, CancellationToken cancellationToken = default) =>
        await estatisticas.SalvarHabilitadosAsync(request.Logins, cancellationToken);
}

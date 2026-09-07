using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Legado;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Aposentadoria dos rascunhos por sistema (tarefa 2.9 do plano 02).
///
/// <para>Os rascunhos do SER e do SERNIT viram solicitações da regulação e as telas antigas
/// ficam somente-leitura. <b>A tabela legada não é apagada aqui</b> — o <c>DropTable</c> é de
/// uma release depois, para haver caminho de volta se a conferência em produção achar algo.</para>
/// </summary>
[ApiController]
[Route("regulacao/legado")]
public sealed class RegulacaoLegadoController(
    IMigradorRascunhosLegadosService migrador,
    IRegulacaoConfiguracaoService configuracao) : ControllerBase
{
    /// <summary>O que a migração faria, sem gravar nada. É por onde se começa.</summary>
    [HttpGet("rascunhos/previa")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<ResultadoMigracaoLegadoDto>(StatusCodes.Status200OK)]
    public Task<ResultadoMigracaoLegadoDto> Previa(CancellationToken cancellationToken) =>
        migrador.PreviaAsync(cancellationToken);

    /// <summary>
    /// Copia os rascunhos para <c>regulacao_solicitacao</c>. Idempotente: rodar de novo só
    /// alcança o que ainda não migrou.
    /// </summary>
    [HttpPost("rascunhos/migrar")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<ResultadoMigracaoLegadoDto>(StatusCodes.Status200OK)]
    public Task<ResultadoMigracaoLegadoDto> Migrar(
        [FromBody] MigrarRascunhosLegadosRequest? corpo, CancellationToken cancellationToken) =>
        migrador.MigrarAsync(corpo ?? new MigrarRascunhosLegadosRequest(null, false), cancellationToken);

    /// <summary>
    /// Reabre as telas antigas para escrita. Existe para o caso de a conferência pós-migração
    /// achar algo que precise ser corrigido na origem.
    /// </summary>
    [HttpPost("rascunhos/reabrir")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reabrir(CancellationToken cancellationToken)
    {
        await configuracao.DefinirRascunhosLegadosMigradosAsync(null, cancellationToken);
        return NoContent();
    }
}

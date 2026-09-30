using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Integracoes.SisregWeb.Indicadores;
using SMSMais.Core.Integracoes.SisregWeb.Indicadores.Background;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Coletor dos <b>Indicadores de Regulação</b> do SISREG, pela tela de Configuração do SISREG: liga e
/// desliga, situação por coletor, retomar depois de CAPTCHA e re-armar falhas.
///
/// <para><b>Nada aqui lê o SISREG na hora.</b> Quem lê é o agendador, um passo por vez, com teto
/// próprio por hora e cedendo a vez aos outros motores.</para>
/// </summary>
[ApiController]
[Route("sisreg/indicadores/coleta")]
public sealed class SisregIndicadoresColetaController(
    IColetaIndicadoresSisregService coleta,
    ColetaIndicadoresEstadoVivo estado,
    IIntegracaoCredencialService credenciais,
    SmsMaisDbContext db,
    Microsoft.Extensions.Options.IOptions<ColetaIndicadoresOpcoes> opcoes) : ControllerBase
{
    [HttpGet("status")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<ColetaIndicadoresStatusDto>(StatusCodes.Status200OK)]
    public async Task<ColetaIndicadoresStatusDto> Status(CancellationToken cancellationToken = default) =>
        await MontarAsync(cancellationToken);

    /// <summary>Liga ou desliga o coletor. Desligar no meio de um item o devolve a pendente.</summary>
    [HttpPut("configuracao")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<ColetaIndicadoresStatusDto>(StatusCodes.Status200OK)]
    public async Task<ColetaIndicadoresStatusDto> Salvar(
        [FromBody] SalvarColetaIndicadoresRequest request, CancellationToken cancellationToken = default)
    {
        await ColetaIndicadoresConfig.SalvarAsync(credenciais, request, cancellationToken);
        return await MontarAsync(cancellationToken);
    }

    /// <summary>Tira a pausa de CAPTCHA — só depois de alguém ter aberto o SISREG no navegador e resolvido.</summary>
    [HttpPost("retomar")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<ColetaIndicadoresStatusDto>(StatusCodes.Status200OK)]
    public async Task<ColetaIndicadoresStatusDto> Retomar(CancellationToken cancellationToken = default)
    {
        await ColetaIndicadoresConfig.PausarAsync(credenciais, null, cancellationToken);
        return await MontarAsync(cancellationToken);
    }

    /// <summary>Todas as janelas em falha voltam a pendente, com as tentativas zeradas.</summary>
    [HttpPost("rearmar")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<ColetaIndicadoresStatusDto>(StatusCodes.Status200OK)]
    public async Task<ColetaIndicadoresStatusDto> Rearmar(CancellationToken cancellationToken = default)
    {
        await coleta.RearmarFalhasAsync(cancellationToken);
        return await MontarAsync(cancellationToken);
    }

    private async Task<ColetaIndicadoresStatusDto> MontarAsync(CancellationToken ct)
    {
        var config = await ColetaIndicadoresConfig.ObterAsync(credenciais, ct);
        var chave = await SincronismoAutomaticoSisreg.LigadoAsync(db, ct);
        var (coletores, falhas) = await coleta.ResumoAsync(ct);
        var agora = DateTime.UtcNow;
        return new ColetaIndicadoresStatusDto(
            config.Ativa,
            config.PausadaAte is { } p && p > agora ? p : null,
            chave,
            config.Ativa ? estado.Espera?.ToString() : null,
            estado.Trabalho?.Descricao,
            estado.Requisicoes.NaUltimaHora(agora),
            opcoes.Value.TetoPorHora,
            estado.UltimoPassoEm,
            coletores,
            falhas);
    }
}

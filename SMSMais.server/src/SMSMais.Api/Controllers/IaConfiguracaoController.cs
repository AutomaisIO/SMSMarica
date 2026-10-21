using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Inteligencia.Configuracao;
using SMSMais.Core.Inteligencia.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Tela de configuração do módulo IA: configuração global (provedor/modelo/tokens) e a lista, só de
/// leitura, das bases que a IA consulta. Cadastrar, editar, testar e gerar token de base é em
/// Sistema → Fontes de dados (<see cref="FontesDadosController"/>, ADR-0076).
/// </summary>
[ApiController]
[Route("ia/configuracao")]
public sealed class IaConfiguracaoController(
    IIaConfiguracaoService configuracaoService,
    IIaFonteService fonteService) : ControllerBase
{
    private readonly IIaConfiguracaoService _configuracaoService = configuracaoService;
    private readonly IIaFonteService _fonteService = fonteService;

    // ---- Configuração global ----

    [HttpGet]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<ConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<ConfiguracaoDto> Obter(CancellationToken cancellationToken) =>
        await _configuracaoService.ObterAsync(cancellationToken);

    [HttpPut]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Atualizar(
        [FromBody] AtualizarConfiguracaoRequest request,
        CancellationToken cancellationToken)
    {
        await _configuracaoService.AtualizarAsync(request, cancellationToken);
        return NoContent();
    }

    // ---- Bases de dados (fontes) ----

    [HttpGet("fontes")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<FonteDetalheDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<FonteDetalheDto>> ListarFontes(CancellationToken cancellationToken) =>
        await _fonteService.ListarAsync(cancellationToken);

    /// <summary>
    /// Auto-provisionamento do agente: o próprio agente chama isto no primeiro run, autenticado com
    /// o login do admin, informando o slug. Fica nesta rota porque o instalador do agente já a chama
    /// (docs/proxy-sql-agente-wss.md); a equivalente nova é <c>fontes-dados/agentes/provisionar</c>.
    /// Cria a base via agente se não existir e devolve o token (uma vez). O agente guarda o token
    /// localmente e a partir daí conecta sozinho. Ver ADR-0023.
    /// </summary>
    [HttpPost("agentes/provisionar")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<TokenAgenteGerado>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<TokenAgenteGerado> ProvisionarAgente(
        [FromBody] ProvisionarAgenteRequest request,
        CancellationToken cancellationToken) =>
        await _fonteService.ProvisionarAgenteAsync(request.Slug, request.Nome, cancellationToken);
}

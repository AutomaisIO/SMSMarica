using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Inteligencia.Configuracao;
using SMSMais.Core.Inteligencia.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Tela de configuração do módulo IA: configuração global (provedor/modelo/tokens) e
/// CRUD das bases de dados (fontes) + teste de conexão. Tokens e senhas são write-only.
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

    [HttpPost("fontes")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CadastrarFonte(
        [FromBody] CadastrarFonteRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _fonteService.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListarFontes), new { id }, id);
    }

    [HttpPut("fontes/{id:guid}")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarFonte(
        Guid id,
        [FromBody] AtualizarFonteRequest request,
        CancellationToken cancellationToken)
    {
        await _fonteService.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("fontes/{id:guid}")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoverFonte(Guid id, CancellationToken cancellationToken)
    {
        await _fonteService.RemoverAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("fontes/{id:guid}/testar-conexao")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<TestarConexaoResultado>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<TestarConexaoResultado> TestarConexao(Guid id, CancellationToken cancellationToken) =>
        await _fonteService.TestarConexaoAsync(id, cancellationToken);

    /// <summary>
    /// Gera (ou rotaciona) o token de conexão do agente proxy de uma base via agente. O token é
    /// devolvido em claro UMA vez — guarde-o no .env do servidor de destino. Ver ADR-0023.
    /// </summary>
    [HttpPost("fontes/{id:guid}/token-agente")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<TokenAgenteGerado>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<TokenAgenteGerado> GerarTokenAgente(Guid id, CancellationToken cancellationToken) =>
        await _fonteService.GerarTokenAgenteAsync(id, cancellationToken);

    /// <summary>
    /// Auto-provisionamento do agente: o próprio agente chama isto no primeiro run, autenticado com
    /// o login do admin, informando o slug. Cria a base via agente se não existir e devolve o token
    /// (uma vez). O agente guarda o token localmente e a partir daí conecta sozinho. Ver ADR-0023.
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

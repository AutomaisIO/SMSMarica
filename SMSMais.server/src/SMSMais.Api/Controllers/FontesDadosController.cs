using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Fontes;
using SMSMais.Core.Fontes.Dtos;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Inteligencia.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Sistema → Fontes de dados (ADR-0077): o cadastro único de toda fonte de onde a plataforma lê
/// dado. A chave da rota é o id da base ou a chave fixa do sistema (<c>sisreg</c>, <c>ser</c>,
/// <c>sernit</c>, <c>esussg</c>, <c>esuspec</c>, <c>sisreg-api</c>). Segredos são write-only.
/// </summary>
[ApiController]
[Route("fontes-dados")]
public sealed class FontesDadosController(IFontesDadosService fontes) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.FontesDados, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<FonteDadosDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<FonteDadosDto>> Listar(CancellationToken cancellationToken) =>
        await fontes.ListarAsync(cancellationToken);

    [HttpGet("{chave}")]
    [RequerPermissao(ModuloPermissao.FontesDados, AcoesPermissao.Consulta)]
    [ProducesResponseType<FonteDadosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<FonteDadosDto> Obter(string chave, CancellationToken cancellationToken) =>
        await fontes.ObterAsync(chave, cancellationToken);

    /// <summary>Cadastra uma BASE. Os sistemas com login são fixos e se editam por PUT.</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.FontesDados, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Criar([FromBody] CriarFonteDadosRequest request, CancellationToken cancellationToken)
    {
        var id = await fontes.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { chave = id.ToString() }, id);
    }

    [HttpPut("{chave}")]
    [RequerPermissao(ModuloPermissao.FontesDados, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        string chave, [FromBody] AtualizarFonteDadosRequest request, CancellationToken cancellationToken)
    {
        await fontes.AtualizarAsync(chave, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Base: exclusão lógica. Sistema: limpa login e senha e desativa (a agenda dos motores fica).</summary>
    [HttpDelete("{chave}")]
    [RequerPermissao(ModuloPermissao.FontesDados, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(string chave, CancellationToken cancellationToken)
    {
        await fontes.RemoverAsync(chave, cancellationToken);
        return NoContent();
    }

    /// <summary>Testa a conexão. Não lança por falha da fonte — devolve sucesso/mensagem.</summary>
    [HttpPost("{chave}/testar")]
    [RequerPermissao(ModuloPermissao.FontesDados, AcoesPermissao.Edicao)]
    [ProducesResponseType<TestarFonteDadosResultado>(StatusCodes.Status200OK)]
    public async Task<TestarFonteDadosResultado> Testar(
        string chave, [FromBody] TestarFonteDadosRequest? request, CancellationToken cancellationToken) =>
        await fontes.TestarAsync(chave, request ?? new TestarFonteDadosRequest(null, null, null), cancellationToken);

    /// <summary>Gera (ou rotaciona) o token do agente de uma base via agente. Mostrado UMA vez (ADR-0023).</summary>
    [HttpPost("{id:guid}/token-agente")]
    [RequerPermissao(ModuloPermissao.FontesDados, AcoesPermissao.Edicao)]
    [ProducesResponseType<TokenAgenteGerado>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<TokenAgenteGerado> GerarTokenAgente(Guid id, CancellationToken cancellationToken) =>
        await fontes.GerarTokenAgenteAsync(id, cancellationToken);

    /// <summary>
    /// Provisiona uma base via agente pelo slug (cria se não existe) e devolve o token uma vez. O
    /// instalador antigo do agente chama a rota equivalente em <c>ia/configuracao/agentes/provisionar</c>,
    /// que continua existindo.
    /// </summary>
    [HttpPost("agentes/provisionar")]
    [RequerPermissao(ModuloPermissao.FontesDados, AcoesPermissao.Edicao)]
    [ProducesResponseType<TokenAgenteGerado>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<TokenAgenteGerado> ProvisionarAgente(
        [FromBody] ProvisionarAgenteRequest request, CancellationToken cancellationToken) =>
        await fontes.ProvisionarAgenteAsync(request.Slug, request.Nome, cancellationToken);
}

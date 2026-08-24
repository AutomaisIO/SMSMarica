using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Erros;
using SMSMais.Core.Erros.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

[ApiController]
[Route("erros")]
public sealed class ErrosController(IRegistroErroService service) : ControllerBase
{
    private readonly IRegistroErroService _service = service;

    /// <summary>
    /// Busca no log de erros não tratados (500), mais recentes primeiro. Filtros
    /// opcionais: código de referência, texto livre (mensagem/caminho/tipo/usuário)
    /// e período. Para diagnóstico do suporte a partir do código informado pelo usuário.
    /// </summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaErrosDto>(StatusCodes.Status200OK)]
    public async Task<PaginaErrosDto> Buscar(
        [FromQuery] string? codigo,
        [FromQuery] string? texto,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50,
        CancellationToken cancellationToken = default) =>
        await _service.BuscarAsync(new ErroFiltroDto(codigo, texto, de, ate, pagina, tamanho), cancellationToken);

    /// <summary>Detalhe completo de um erro (com stack trace) pelo código de referência.</summary>
    [HttpGet("{codigo}")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Consulta)]
    [ProducesResponseType<RegistroErroDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorCodigo(string codigo, CancellationToken cancellationToken)
    {
        var erro = await _service.ObterPorCodigoAsync(codigo, cancellationToken);
        return erro is null ? NotFound() : Ok(erro);
    }

    /// <summary>
    /// Marca o erro como resolvido (data + autor + nota). Usado pela skill de suporte quando o bug
    /// é corrigido; reincidências posteriores geram um código novo (sinal de regressão).
    /// </summary>
    [HttpPost("{codigo}/resolver")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegistroErroDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolver(
        string codigo, [FromBody] ResolverErroRequest? request, CancellationToken cancellationToken)
    {
        var erro = await _service.ResolverAsync(codigo, request ?? new ResolverErroRequest(), cancellationToken);
        return erro is null ? NotFound() : Ok(erro);
    }

    /// <summary>Reabre um erro resolvido (limpa a marcação de resolução).</summary>
    [HttpPost("{codigo}/reabrir")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegistroErroDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reabrir(string codigo, CancellationToken cancellationToken)
    {
        var erro = await _service.ReabrirAsync(codigo, cancellationToken);
        return erro is null ? NotFound() : Ok(erro);
    }
}

using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Erros;
using SMSMarica.Core.Erros.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

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
}

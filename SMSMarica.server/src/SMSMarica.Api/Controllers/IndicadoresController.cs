using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Indicadores;
using SMSMarica.Core.Indicadores.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Indicadores contratuais do HMCML. Cada indicador tem um motor (SQL) que roda read-only
/// contra a base de origem; a apuração é sob demanda e o resultado fica gravado com a versão
/// do SQL que o produziu. Ver ADR-0022.
/// </summary>
[ApiController]
[Route("indicadores")]
public sealed class IndicadoresController(IIndicadoresService service) : ControllerBase
{
    private readonly IIndicadoresService _service = service;

    /// <summary>Unidades do filtro do topo (hoje só o Conde Modesto Leal).</summary>
    [HttpGet("unidades")]
    [RequerPermissao(ModuloPermissao.Indicadores, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<UnidadeIndicadorDto>>(StatusCodes.Status200OK)]
    public IReadOnlyList<UnidadeIndicadorDto> Unidades() => _service.Unidades();

    [HttpGet("abas/{aba}")]
    [RequerPermissao(ModuloPermissao.Indicadores, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<IndicadorResumoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<IndicadorResumoDto>> Listar(
        AbaIndicador aba,
        [FromQuery] int hospital,
        [FromQuery] DateOnly inicio,
        [FromQuery] DateOnly fim,
        CancellationToken cancellationToken) =>
        await _service.ListarAsync(aba, new FiltroIndicadorDto(hospital, inicio, fim), cancellationToken);

    /// <summary>Apura todos os indicadores com motor da aba. Roda em sequência no Oracle.</summary>
    [HttpPost("abas/{aba}/apurar")]
    [RequerPermissao(ModuloPermissao.Indicadores, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<IndicadorResumoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<IndicadorResumoDto>> ApurarAba(
        AbaIndicador aba,
        [FromBody] FiltroIndicadorDto filtro,
        CancellationToken cancellationToken) =>
        await _service.ApurarAbaAsync(aba, filtro, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Indicadores, AcoesPermissao.Consulta)]
    [ProducesResponseType<IndicadorDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IndicadorDetalheDto> Obter(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterAsync(id, cancellationToken);

    [HttpGet("{id:guid}/versoes")]
    [RequerPermissao(ModuloPermissao.Indicadores, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<IndicadorVersaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<IndicadorVersaoDto>> Versoes(Guid id, CancellationToken cancellationToken) =>
        await _service.ListarVersoesAsync(id, cancellationToken);

    /// <summary>Apura um indicador. <c>previa=true</c> não grava histórico (uso da tela de edição).</summary>
    [HttpPost("{id:guid}/apurar")]
    [RequerPermissao(ModuloPermissao.Indicadores, AcoesPermissao.Consulta)]
    [ProducesResponseType<ResultadoIndicadorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ResultadoIndicadorDto> Apurar(
        Guid id,
        [FromBody] FiltroIndicadorDto filtro,
        [FromQuery] bool previa,
        CancellationToken cancellationToken) =>
        await _service.ApurarAsync(id, filtro, persistir: !previa, cancellationToken);

    /// <summary>
    /// Relatório analítico do indicador: os registros que entraram na conta e os que foram
    /// excluídos (com o motivo), do jeito que o SQL analítico os devolveu. Consulta a base de
    /// origem na hora e não grava nada — é a evidência do número, não um resultado novo.
    /// </summary>
    [HttpPost("{id:guid}/analitico")]
    [RequerPermissao(ModuloPermissao.Indicadores, AcoesPermissao.Consulta)]
    [ProducesResponseType<AnaliticoIndicadorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AnaliticoIndicadorDto> Analitico(
        Guid id,
        [FromBody] FiltroIndicadorDto filtro,
        CancellationToken cancellationToken) =>
        await _service.AnaliticoAsync(id, filtro, cancellationToken);

    /// <summary>Bases de dados onde um motor pode rodar.</summary>
    [HttpGet("fontes")]
    [RequerPermissao(ModuloPermissao.Indicadores, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<FonteIndicadorDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<FonteIndicadorDto>> Fontes(CancellationToken cancellationToken) =>
        await _service.ListarFontesAsync(cancellationToken);

    /// <summary>Cadastra um indicador novo — a planilha contratual pode ganhar linhas.</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.Indicadores, AcoesPermissao.Inclusao)]
    [ProducesResponseType<IndicadorDetalheDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IndicadorDetalheDto>> Criar(
        [FromBody] SalvarIndicadorDto dto,
        CancellationToken cancellationToken)
    {
        var criado = await _service.CriarAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
    }

    /// <summary>
    /// Grava o cadastro inteiro: meta, peso, memória de cálculo e SQL. Meta e pontuação são
    /// cláusula de contrato e mudam entre versões da planilha — por isso são editáveis aqui,
    /// sem deploy. O SQL anterior é versionado quando muda.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Indicadores, AcoesPermissao.Edicao)]
    [ProducesResponseType<IndicadorDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IndicadorDetalheDto> Atualizar(
        Guid id,
        [FromBody] SalvarIndicadorDto dto,
        CancellationToken cancellationToken) =>
        await _service.AtualizarAsync(id, dto, cancellationToken);

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Indicadores, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        await _service.ExcluirAsync(id, cancellationToken);
        return NoContent();
    }
}

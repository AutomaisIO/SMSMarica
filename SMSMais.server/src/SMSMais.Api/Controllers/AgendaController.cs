using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.AgendaRegulacao;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// A Agenda: oferta de vagas do SISREG × ocupação já importada.
///
/// <para>O grão é <b>unidade × profissional × dia</b> — não entra procedimento na chave do
/// cruzamento porque a escala é publicada em grupos que se expandem em itens no agendamento, e
/// casar por procedimento perderia metade dos casos. O procedimento aparece no detalhe do dia.</para>
/// </summary>
[ApiController]
[Route("agenda")]
public sealed class AgendaController(
    IAgendaAnaliseService agenda,
    IAgendaDemandaService demanda) : ControllerBase
{
    private readonly IAgendaAnaliseService _agenda = agenda;
    private readonly IAgendaDemandaService _demanda = demanda;

    /// <summary>Opções dos filtros — só quem ainda tem escala vigente.</summary>
    [HttpGet("opcoes")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<AgendaOpcoesDto>(StatusCodes.Status200OK)]
    public async Task<AgendaOpcoesDto> Opcoes(CancellationToken ct) =>
        await _agenda.OpcoesAsync(ct);

    /// <summary>Os números do topo: oferta, ocupação, ociosidade e sobrecarga.</summary>
    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<AgendaResumoDto>(StatusCodes.Status200OK)]
    public async Task<AgendaResumoDto> Resumo([FromQuery] AgendaFiltro filtro, CancellationToken ct) =>
        await _agenda.ResumoAsync(filtro, ct);

    /// <summary>Um dia de um profissional por linha. Inclui dia COM agendamento e SEM escala.</summary>
    [HttpGet("dias")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaAgendaDto>(StatusCodes.Status200OK)]
    public async Task<PaginaAgendaDto> Dias(
        [FromQuery] AgendaFiltro filtro,
        [FromQuery] int pagina = 0,
        [FromQuery] int tamanho = 100,
        CancellationToken ct = default) =>
        await _agenda.ListarDiasAsync(filtro, pagina, tamanho, ct);

    /// <summary>O que foi publicado naquele dia, quem ocupa, e a grade de horários deduzida.</summary>
    [HttpGet("dia")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<AgendaDiaDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AgendaDiaDetalheDto> Dia(
        [FromQuery] Guid unidadeId,
        [FromQuery] string profissionalCpf,
        [FromQuery] DateOnly data,
        CancellationToken ct) =>
        await _agenda.DetalharDiaAsync(unidadeId, profissionalCpf, data, ct);

    /// <summary>Ranking por unidade, especialidade (CBO) ou profissional.</summary>
    [HttpGet("ranking")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AgendaRankingItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AgendaRankingItemDto>> Ranking(
        [FromQuery] AgendaFiltro filtro,
        [FromQuery] string eixo = "unidade",
        [FromQuery] int limite = 20,
        CancellationToken ct = default) =>
        await _agenda.RankingAsync(filtro, eixo, limite, ct);

    /// <summary>Como a oferta e a ocupação se distribuem pelos dias da semana.</summary>
    [HttpGet("dias-semana")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AgendaPorDiaSemanaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AgendaPorDiaSemanaDto>> DiasSemana(
        [FromQuery] AgendaFiltro filtro, CancellationToken ct) =>
        await _agenda.PorDiaSemanaAsync(filtro, ct);

    /// <summary>Oferta × ocupação dia a dia — a série que a tela plota.</summary>
    [HttpGet("serie")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AgendaSerieDiaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AgendaSerieDiaDto>> Serie(
        [FromQuery] AgendaFiltro filtro, CancellationToken ct) =>
        await _agenda.SerieAsync(filtro, ct);

    /// <summary>
    /// Até onde o dado existe. Toda tela do módulo depende disto para não apresentar ausência de
    /// importação como se fosse ausência de movimento.
    /// </summary>
    [HttpGet("cobertura")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<AgendaCoberturaDto>(StatusCodes.Status200OK)]
    public async Task<AgendaCoberturaDto> Cobertura(CancellationToken ct) =>
        await _demanda.CoberturaAsync(ct);

    // ---------------------------------------------------------------- demanda

    /// <summary>Opções dos filtros da demanda — o que aparece em solicitação no último ano.</summary>
    [HttpGet("demanda/opcoes")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<DemandaOpcoesDto>(StatusCodes.Status200OK)]
    public async Task<DemandaOpcoesDto> DemandaOpcoes(CancellationToken ct) =>
        await _demanda.OpcoesAsync(ct);

    /// <summary>Volume regulado e tempo de espera no recorte.</summary>
    [HttpGet("demanda/resumo")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<DemandaResumoDto>(StatusCodes.Status200OK)]
    public async Task<DemandaResumoDto> DemandaResumo(
        [FromQuery] DemandaFiltro filtro, CancellationToken ct) =>
        await _demanda.ResumoAsync(filtro, ct);

    /// <summary>
    /// Top procedimentos regulados. <c>ordenarPor</c>: <c>volume</c> (padrão), <c>espera</c> ou
    /// <c>atraso</c> — ordenar por espera é o que encontra o gargalo, que raramente é o de maior
    /// volume.
    /// </summary>
    [HttpGet("demanda/procedimentos")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<DemandaProcedimentoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<DemandaProcedimentoDto>> DemandaProcedimentos(
        [FromQuery] DemandaFiltro filtro,
        [FromQuery] string ordenarPor = "volume",
        [FromQuery] int limite = 20,
        CancellationToken ct = default) =>
        await _demanda.ProcedimentosAsync(filtro, ordenarPor, limite, ct);

    /// <summary>Histograma da espera em faixas fixas.</summary>
    [HttpGet("demanda/faixas-espera")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<DemandaFaixaEsperaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<DemandaFaixaEsperaDto>> DemandaFaixas(
        [FromQuery] DemandaFiltro filtro, CancellationToken ct) =>
        await _demanda.FaixasEsperaAsync(filtro, ct);

    /// <summary>De onde vem o pedido (<c>solicitante</c>) ou para onde vai (<c>executante</c>).</summary>
    [HttpGet("demanda/origem")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<DemandaOrigemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<DemandaOrigemDto>> DemandaOrigem(
        [FromQuery] DemandaFiltro filtro,
        [FromQuery] string eixo = "solicitante",
        [FromQuery] int limite = 15,
        CancellationToken ct = default) =>
        await _demanda.OrigemAsync(filtro, eixo, limite, ct);

    /// <summary>Volume e espera mês a mês.</summary>
    [HttpGet("demanda/serie")]
    [RequerPermissao(ModuloPermissao.Agenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<DemandaSerieDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<DemandaSerieDto>> DemandaSerie(
        [FromQuery] DemandaFiltro filtro, CancellationToken ct) =>
        await _demanda.SerieAsync(filtro, ct);
}

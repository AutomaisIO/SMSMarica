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
public sealed class AgendaController(IAgendaAnaliseService agenda) : ControllerBase
{
    private readonly IAgendaAnaliseService _agenda = agenda;

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
}

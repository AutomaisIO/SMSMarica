using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Regulacao.Indicadores;
using SMSMais.Core.Regulacao.Indicadores.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Regulação → <b>Indicadores</b> de um sistema (SISREG, SER, SERNIT, ESUS de São Gonçalo): série mensal de
/// vagas, absenteísmo, regulados, fila, desfechos com motivos, espera e judicializadas, com o selo de
/// origem em cada número. Somente leitura sobre o nosso banco — nada aqui fala com os sistemas de origem.
/// Ver: <see cref="ModuloPermissao.IndicadoresRegulacao"/>.
/// </summary>
[ApiController]
[Route("regulacao/indicadores")]
public sealed class IndicadoresRegulacaoController(IIndicadoresRegulacaoService indicadores) : ControllerBase
{
    /// <summary>Indicadores do sistema no período.</summary>
    /// <param name="fonte"><c>sisreg</c>, <c>ser</c>, <c>sernit</c> ou <c>esussg</c>.</param>
    /// <param name="inicio">Primeiro mês (<c>AAAA-MM</c>). Sem período: os 12 meses fechados até o mês anterior.</param>
    /// <param name="fim">Último mês (<c>AAAA-MM</c>), fechado. Até 24 meses no total.</param>
    [HttpGet("{fonte}")]
    [RequerPermissao(ModuloPermissao.IndicadoresRegulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IndicadoresRegulacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IndicadoresRegulacaoDto> Obter(
        string fonte, [FromQuery] string? inicio, [FromQuery] string? fim, CancellationToken cancellationToken = default) =>
        await indicadores.ObterAsync(Fonte(fonte), Mes(inicio, "inicio"), Mes(fim, "fim"), cancellationToken);

    /// <summary>Os mesmos indicadores em PDF (A4 paisagem, com a identidade visual da instituição).</summary>
    [HttpGet("{fonte}/pdf")]
    [RequerPermissao(ModuloPermissao.IndicadoresRegulacao, AcoesPermissao.Consulta)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pdf(
        string fonte, [FromQuery] string? inicio, [FromQuery] string? fim, CancellationToken cancellationToken = default)
    {
        var f = Fonte(fonte);
        var pdf = await indicadores.PdfAsync(f, Mes(inicio, "inicio"), Mes(fim, "fim"), cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(pdf, "application/pdf", $"indicadores-regulacao-{fonte.ToLowerInvariant()}.pdf");
    }

    private static FonteIndicadorRegulacao Fonte(string fonte) => fonte.ToLowerInvariant() switch
    {
        "sisreg" => FonteIndicadorRegulacao.Sisreg,
        "ser" => FonteIndicadorRegulacao.Ser,
        "sernit" => FonteIndicadorRegulacao.Sernit,
        "esussg" => FonteIndicadorRegulacao.EsusSg,
        _ => throw new ValidacaoException("fonte", "Sistema desconhecido: use sisreg, ser, sernit ou esussg."),
    };

    private static DateOnly? Mes(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (DateOnly.TryParseExact(valor + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        throw new ValidacaoException(campo, $"'{campo}' deve estar no formato AAAA-MM.");
    }
}

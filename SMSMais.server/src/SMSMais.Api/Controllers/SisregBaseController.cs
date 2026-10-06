using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.Sisreg.Base;
using SMSMais.Core.Integracoes.Sisreg.Base.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Consulta dos agendamentos do SISREG <b>na nossa base</b> (tela SISREG → Consultar). Não fala
/// com o SISREG: lê o que a importação e o sincronismo diário já trouxeram, com a situação de cada
/// atendimento (na fila, agendada, pendente de atualização, compareceu, faltou, cancelada).
/// O filtro vai no corpo porque leva listas (unidades, situações, procedimentos).
/// </summary>
[ApiController]
[Route("sisreg/base")]
public sealed class SisregBaseController(IConsultaBaseSisregService consulta) : ControllerBase
{
    /// <summary>Uma página do resultado + totais (atendimentos, pessoas distintas, por situação).</summary>
    [HttpPost("agendamentos/buscar")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<ConsultaBaseSisregResultado>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ConsultaBaseSisregResultado> Buscar(
        [FromBody] ConsultaBaseSisregFiltro filtro, CancellationToken cancellationToken) =>
        await consulta.BuscarAsync(filtro, cancellationToken);

    /// <summary>Unidades executantes e procedimentos com atendimento no período, para os filtros.</summary>
    [HttpGet("opcoes")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<OpcoesConsultaBaseSisregDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<OpcoesConsultaBaseSisregDto> Opcoes(
        [FromQuery] DateOnly inicio,
        [FromQuery] DateOnly fim,
        [FromQuery] EixoDataConsultaSisreg eixo = EixoDataConsultaSisreg.Agendamento,
        CancellationToken cancellationToken = default) =>
        await consulta.OpcoesAsync(inicio, fim, eixo, cancellationToken);

    /// <summary>O resultado inteiro do filtro em PDF nominal (paciente, data, procedimento, situação).</summary>
    [HttpPost("agendamentos/pdf")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pdf([FromBody] ConsultaBaseSisregFiltro filtro, CancellationToken cancellationToken)
    {
        var pdf = await consulta.PdfAsync(filtro, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(pdf, "application/pdf", $"sisreg-atendimentos-{filtro.Inicio:yyyyMMdd}-{filtro.Fim:yyyyMMdd}.pdf");
    }
}

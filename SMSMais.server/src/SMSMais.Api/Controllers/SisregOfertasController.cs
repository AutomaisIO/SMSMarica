using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.SisregWeb.Ofertas;
using SMSMais.Core.Integracoes.SisregWeb.Ofertas.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// <b>Ofertas</b>: o que abriu no SISREG — agenda nova e vaga liberada por cancelamento.
///
/// <para>Somente leitura, e nada aqui fala com o SISREG: lê o que a varredura e o sincronismo de
/// escalas já trouxeram.</para>
///
/// <para>Usa a permissão <see cref="ModuloPermissao.AlteracoesAgenda"/> de propósito, em vez de um
/// módulo novo: as vagas liberadas <b>são</b> as alterações do tipo Ausente, lidas pelo lado da
/// oportunidade. Quem pode ver uma tem de ver a outra — separar as permissões deixaria alguém
/// vendo "sumiu do SISREG" sem ver "vagou", que é a mesma linha.</para>
/// </summary>
[ApiController]
[Route("sisreg/ofertas")]
public sealed class SisregOfertasController(IOfertasSisregService ofertas) : ControllerBase
{
    /// <summary>Agendas que nasceram na janela pedida e vagas liberadas ainda no futuro.</summary>
    /// <param name="dias">Janela de novidade das agendas, em dias (1 a 90; padrão 7).</param>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.AlteracoesAgenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<OfertasSisregDto>(StatusCodes.Status200OK)]
    public async Task<OfertasSisregDto> Listar(
        [FromQuery] int dias = 7, CancellationToken cancellationToken = default) =>
        await ofertas.ListarAsync(dias, cancellationToken);

    /// <summary>
    /// Quem está esperando por este procedimento — a lista que a oferta destrava.
    /// </summary>
    /// <param name="procedimento">Nome exato, como o SISREG escreve. É o eixo: esta tela do SISREG
    /// não manda código de procedimento.</param>
    /// <param name="ordenar"><c>espera</c> (padrão), <c>risco</c>, <c>idade</c> ou <c>nome</c>.</param>
    [HttpGet("fila")]
    [RequerPermissao(ModuloPermissao.AlteracoesAgenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<FilaDaOfertaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<FilaDaOfertaDto> Fila(
        [FromQuery] string procedimento,
        [FromQuery] string? ordenar = null,
        [FromQuery] int limite = 100,
        [FromQuery] int pulo = 0,
        CancellationToken cancellationToken = default) =>
        await ofertas.FilaDaOfertaAsync(procedimento, ordenar, limite, pulo, cancellationToken);
}

using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.SisregWeb.Fila;
using SMSMais.Core.Integracoes.SisregWeb.Fila.Background;
using SMSMais.Core.Integracoes.SisregWeb.Ofertas;
using SMSMais.Core.Integracoes.SisregWeb.Ofertas.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// <b>Ofertas</b>: o que abriu no SISREG — agenda nova e vaga liberada por cancelamento.
///
/// <para>Somente leitura, sobre o que a varredura, o sincronismo de escalas e o motor da fila já
/// trouxeram. Disparar a leitura da fila fica na Configuração do SISREG
/// (<see cref="SisregFilaController"/>); aqui só se mostra a situação dela.</para>
///
/// <para>Usa a permissão <see cref="ModuloPermissao.AlteracoesAgenda"/> de propósito, em vez de um
/// módulo novo: as vagas liberadas <b>são</b> as alterações do tipo Ausente, lidas pelo lado da
/// oportunidade. Quem pode ver uma tem de ver a outra — separar as permissões deixaria alguém
/// vendo "sumiu do SISREG" sem ver "vagou", que é a mesma linha.</para>
/// </summary>
[ApiController]
[Route("sisreg/ofertas")]
public sealed class SisregOfertasController(
    IOfertasSisregService ofertas,
    IFilaPendenteSisregService fila,
    FilaPendenteEstadoVivo estadoFila) : ControllerBase
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
    /// As datas do procedimento, por unidade executante: dias com vaga de primeira vez, quantas
    /// estão livres e a primeira data em que dá para marcar.
    /// </summary>
    /// <param name="procedimentoCodigo">Código do SISREG (7 dígitos). Item casa também com a escala
    /// do grupo dele; grupo cobre todos os itens.</param>
    /// <param name="dias">Horizonte olhado a partir de hoje (7 a 180; padrão 120).</param>
    [HttpGet("datas")]
    [RequerPermissao(ModuloPermissao.AlteracoesAgenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<DatasDaOfertaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<DatasDaOfertaDto> Datas(
        [FromQuery] string procedimentoCodigo,
        [FromQuery] int dias = 120,
        CancellationToken cancellationToken = default) =>
        await ofertas.DatasDaOfertaAsync(procedimentoCodigo, dias, cancellationToken);

    /// <summary>
    /// Quem está esperando por este procedimento — a lista que a oferta destrava.
    /// </summary>
    /// <param name="procedimento">Nome exato, como o SISREG escreve. É o eixo: a tela da fila do
    /// SISREG não manda código de procedimento.</param>
    /// <param name="ordenar"><c>espera</c> (padrão), <c>risco</c>, <c>idade</c> ou <c>nome</c>.</param>
    /// <param name="procedimentoCodigo">Opcional: com ele, a fila inclui quem pediu o grupo (vaga de
    /// item) ou qualquer item do grupo (vaga de grupo).</param>
    [HttpGet("fila")]
    [RequerPermissao(ModuloPermissao.AlteracoesAgenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<FilaDaOfertaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<FilaDaOfertaDto> Fila(
        [FromQuery] string procedimento,
        [FromQuery] string? ordenar = null,
        [FromQuery] int limite = 100,
        [FromQuery] int pulo = 0,
        [FromQuery] string? procedimentoCodigo = null,
        CancellationToken cancellationToken = default) =>
        await ofertas.FilaDaOfertaAsync(
            procedimento, ordenar, limite, pulo, procedimentoCodigo, cancellationToken);

    /// <summary>Situação da leitura da fila — só leitura; disparar é na Configuração do SISREG.</summary>
    [HttpGet("fila/status")]
    [RequerPermissao(ModuloPermissao.AlteracoesAgenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<FilaCargaStatusDto>(StatusCodes.Status200OK)]
    public async Task<FilaCargaStatusDto> StatusDaFila(CancellationToken cancellationToken = default) =>
        estadoFila.Snapshot(await fila.ResumoAsync(cancellationToken));
}

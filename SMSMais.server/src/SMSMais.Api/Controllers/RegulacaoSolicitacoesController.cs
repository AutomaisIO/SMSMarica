using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Abertura e edição da solicitação pela unidade solicitante (planos 02 e 04).
///
/// <para><b>Nenhum endpoint aqui escreve em SISREG, SER ou SERNIT</b> (D-11). "Enviar para a
/// fila" põe a solicitação em pré-regulação e para; o envio ao sistema é do agente regulador
/// (módulo 48) e entra nos incrementos 3, 5 e 7.</para>
/// </summary>
[ApiController]
[Route("regulacao/solicitacoes")]
public sealed class RegulacaoSolicitacoesController(
    IRegulacaoSolicitacaoService servico,
    IRegulacaoFormularioService formularios) : ControllerBase
{
    /// <summary>
    /// A fila. Quem tem só o módulo 47 vê as solicitações das suas unidades; quem tem o 48 (agente
    /// regulador) vê o município inteiro — a ampliação é do serviço, não deste atributo.
    /// </summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaSolicitacoesRegulacaoDto>(StatusCodes.Status200OK)]
    public Task<PaginaSolicitacoesRegulacaoDto> Listar(
        [FromQuery] RegulacaoSolicitacaoFiltro filtro, CancellationToken cancellationToken) =>
        servico.ListarAsync(filtro, cancellationToken);

    /// <summary>Contagem por status — as abas da fila e o badge da sidebar saem daqui.</summary>
    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<RegulacaoResumoFilaDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoResumoFilaDto> Resumo(CancellationToken cancellationToken) =>
        servico.ResumoAsync(cancellationToken);

    /// <summary>
    /// A história do caso: quem fez o quê, de qual estado para qual, com o que mudou. Passa pelo
    /// mesmo escopo do detalhe — a linha do tempo é dado de paciente.
    /// </summary>
    [HttpGet("{id:guid}/eventos")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<RegulacaoEventoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<RegulacaoEventoDto>> Eventos(Guid id, CancellationToken cancellationToken) =>
        servico.EventosAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Inclusao)]
    [ProducesResponseType<RegulacaoSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoSolicitacaoDetalheDto> Criar(
        [FromBody] CriarRegulacaoSolicitacaoRequest req, CancellationToken cancellationToken) =>
        servico.CriarAsync(req, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<RegulacaoSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoSolicitacaoDetalheDto> Obter(Guid id, CancellationToken cancellationToken) =>
        servico.ObterAsync(id, cancellationToken);

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegulacaoSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoSolicitacaoDetalheDto> Atualizar(
        Guid id, [FromBody] AtualizarRegulacaoSolicitacaoRequest req, CancellationToken cancellationToken) =>
        servico.AtualizarAsync(id, req, cancellationToken);

    /// <summary>
    /// Definição do formulário daquele procedimento e fluxo. A tela desenha a partir daqui, e é
    /// a mesma versão que o envio usa para traduzir — por isso vem com o `versaoId`.
    /// </summary>
    [HttpGet("formulario")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<RegulacaoFormularioDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoFormularioDto> Formulario(
        [FromQuery] Guid procedimentoId, [FromQuery] FluxoRegulacao fluxo,
        CancellationToken cancellationToken) =>
        formularios.ObterOuGerarAsync(procedimentoId, fluxo, cancellationToken);

    /// <summary>O que ainda falta para a solicitação sair do rascunho. Lista vazia = pode enviar.</summary>
    [HttpGet("{id:guid}/pendencias")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<PendenciaEnvioDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<PendenciaEnvioDto>> Pendencias(Guid id, CancellationToken cancellationToken) =>
        servico.PendenciasDeEnvioAsync(id, cancellationToken);

    [HttpPost("{id:guid}/enviar-fila")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegulacaoSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoSolicitacaoDetalheDto> EnviarParaFila(Guid id, CancellationToken cancellationToken) =>
        servico.EnviarParaFilaAsync(id, cancellationToken);

    [HttpPost("{id:guid}/cancelar")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancelar(
        Guid id, [FromBody] CancelarRequest req, CancellationToken cancellationToken)
    {
        await servico.CancelarAsync(id, req.Motivo, cancellationToken);
        return NoContent();
    }

    public sealed record CancelarRequest(string Motivo);
}

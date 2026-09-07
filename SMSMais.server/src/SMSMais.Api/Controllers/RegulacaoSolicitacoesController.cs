using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Core.Regulacao.Regras;
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
    IRegulacaoFormularioService formularios,
    IRegulacaoElegibilidadeService elegibilidade) : ControllerBase
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

    // ---------------------------------------------------------------- agente regulador (48)

    /// <summary>
    /// Assume o caso. Concorrência resolvida no banco: dois agentes clicando junto, um ganha e o
    /// outro recebe 409 — em vez de os dois acharem que assumiram.
    /// </summary>
    [HttpPost("{id:guid}/assumir")]
    [RequerPermissao(ModuloPermissao.RegulacaoTriagem, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegulacaoSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<RegulacaoSolicitacaoDetalheDto> Assumir(Guid id, CancellationToken cancellationToken) =>
        servico.AssumirAsync(id, cancellationToken);

    /// <summary>Devolve à unidade com o motivo — a ponta corrige e reenvia.</summary>
    [HttpPost("{id:guid}/devolver")]
    [RequerPermissao(ModuloPermissao.RegulacaoTriagem, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegulacaoSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoSolicitacaoDetalheDto> Devolver(
        Guid id, [FromBody] MotivoRequest req, CancellationToken cancellationToken) =>
        servico.DevolverAsync(id, req.Motivo, cancellationToken);

    /// <summary>Recusa em definitivo. É `Exclusao` porque encerra o caso sem atendimento.</summary>
    [HttpPost("{id:guid}/recusar")]
    [RequerPermissao(ModuloPermissao.RegulacaoTriagem, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Recusar(
        Guid id, [FromBody] MotivoRequest req, CancellationToken cancellationToken)
    {
        await servico.RecusarAsync(id, req.Motivo, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Envio assistido: o agente incluiu pela tela do sistema de regulação e digita o número
    /// aqui. <b>Nada sai daqui para o SISREG/SER/SERNIT</b> — a D-11 continua de pé.
    /// </summary>
    [HttpPost("{id:guid}/registrar-envio")]
    [RequerPermissao(ModuloPermissao.RegulacaoTriagem, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegulacaoSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<RegulacaoSolicitacaoDetalheDto> RegistrarEnvio(
        Guid id, [FromBody] RegistrarEnvioRequest req, CancellationToken cancellationToken) =>
        servico.RegistrarEnvioAsync(id, req, cancellationToken);

    /// <summary>D-8: confere a solicitação interna que o solicitante já incluiu no SISREG.</summary>
    [HttpPost("{id:guid}/ok-interno")]
    [RequerPermissao(ModuloPermissao.RegulacaoTriagem, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegulacaoSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoSolicitacaoDetalheDto> OkInterno(Guid id, CancellationToken cancellationToken) =>
        servico.ConfirmarOkInternoAsync(id, cancellationToken);

    /// <summary>
    /// D-10: troca o procedimento canônico. Não é um ajuste comum — muda o formulário e as regras,
    /// então o serviço regera a versão, preserva o que sobrevive e registra o que caiu.
    /// </summary>
    [HttpPost("{id:guid}/trocar-procedimento")]
    [RequerPermissao(ModuloPermissao.RegulacaoTriagem, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegulacaoSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<RegulacaoSolicitacaoDetalheDto> TrocarProcedimento(
        Guid id, [FromBody] TrocarProcedimentoRequest req, CancellationToken cancellationToken) =>
        servico.TrocarProcedimentoAsync(id, req.ProcedimentoId, cancellationToken);

    // ---------------------------------------------------------------- elegibilidade (plano 03)

    /// <summary>
    /// O que as regras do manual dizem sobre este pedido: destinos permitidos, o que bloqueia,
    /// as perguntas que faltam e as caixinhas de documento. Avaliar também **persiste** o
    /// veredito — é ele que a fila e o envio consultam.
    /// </summary>
    [HttpGet("{id:guid}/elegibilidade")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<AvaliacaoElegibilidadeDto>(StatusCodes.Status200OK)]
    public Task<AvaliacaoElegibilidadeDto> Elegibilidade(Guid id, CancellationToken cancellationToken) =>
        elegibilidade.AvaliarAsync(id, cancellationToken);

    /// <summary>Responde o questionário e reavalia na mesma chamada.</summary>
    [HttpPut("{id:guid}/respostas")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Edicao)]
    [ProducesResponseType<AvaliacaoElegibilidadeDto>(StatusCodes.Status200OK)]
    public Task<AvaliacaoElegibilidadeDto> Responder(
        Guid id, [FromBody] ResponderRegrasRequest req, CancellationToken cancellationToken) =>
        elegibilidade.ResponderAsync(id, req.Respostas, cancellationToken);

    /// <summary>Exames que o próprio SMSMais já tem e servem para aquela caixinha.</summary>
    [HttpGet("{id:guid}/exigencias/{exigenciaId:guid}/exames-internos")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ExameParaRegras>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ExameParaRegras>> ExamesInternos(
        Guid id, Guid exigenciaId, CancellationToken cancellationToken) =>
        elegibilidade.ExamesInternosAsync(id, exigenciaId, cancellationToken);

    public sealed record ResponderRegrasRequest(
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao> Respostas);

    public sealed record TrocarProcedimentoRequest(Guid ProcedimentoId);

    public sealed record CancelarRequest(string Motivo);

    public sealed record MotivoRequest(string Motivo);
}

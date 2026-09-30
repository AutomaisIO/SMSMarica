using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.EsusSg;
using SMSMais.Core.EsusSg.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Core.Regulacao.AnaliseRegras;
using SMSMais.Core.Regulacao.AnaliseRegras.Dtos;
using SMSMais.Core.Regulacao.Estatisticas;
using SMSMais.Core.Regulacao.Estatisticas.Dtos;
using SMSMais.Core.Regulacao.Notificacoes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Espelho da fila e dos agendados de Maricá no <b>ESUS de São Gonçalo</b> (ADR-0063) — o produto
/// ESUS, não o e-SUS do governo. Só leitura: lê o NOSSO banco, nunca o ESUS ao vivo.
/// </summary>
[ApiController]
[Route("regulacao/esussg")]
public sealed class EsusSgController(IEsusSgConsultaService consulta) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoEsusSg, AcoesPermissao.Consulta)]
    [ProducesResponseType<EsusSgBuscaResultadoDto>(StatusCodes.Status200OK)]
    public Task<EsusSgBuscaResultadoDto> Buscar(
        [FromQuery] EsusSgBuscaFiltroDto filtro, CancellationToken cancellationToken) =>
        consulta.BuscarAsync(filtro, cancellationToken);

    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.RegulacaoEsusSg, AcoesPermissao.Consulta)]
    [ProducesResponseType<EsusSgResumoDto>(StatusCodes.Status200OK)]
    public Task<EsusSgResumoDto> Resumo(CancellationToken cancellationToken) =>
        consulta.ResumoAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoEsusSg, AcoesPermissao.Consulta)]
    [ProducesResponseType<EsusSgSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<EsusSgSolicitacaoDetalheDto> Obter(Guid id, CancellationToken cancellationToken) =>
        consulta.ObterAsync(id, cancellationToken);

    /// <summary>Refaz agora a análise de regras deste pedido (ADR-0063 §4). Só recalcula o NOSSO
    /// parecer — não escreve nada no ESUS.</summary>
    [HttpPost("{id:guid}/analise/reanalisar")]
    [RequerPermissao(ModuloPermissao.RegulacaoEsusSg, AcoesPermissao.Consulta)]
    [ProducesResponseType<AnaliseRegrasDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnaliseRegrasDetalheDto>> Reanalisar(
        Guid id, [FromServices] IAnaliseRegrasEspelhoService analise, CancellationToken cancellationToken) =>
        await analise.ReanalisarAsync(SistemaRegulacao.EsusSg, id, cancellationToken) is { } d ? d : NotFound();
}

/// <summary>Notificações do ESUS SG — consumidor da fila <c>esussg_gatilho</c>.</summary>
[ApiController]
[Route("regulacao/esussg/notificacoes")]
public sealed class EsusSgNotificacaoController(IEsusSgNotificacaoService notificacoes) : ControllerBase
{
    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.RegulacaoEsusSg, AcoesPermissao.Consulta)]
    [ProducesResponseType<EsusSgNotificacaoResumoDto>(StatusCodes.Status200OK)]
    public Task<EsusSgNotificacaoResumoDto> Resumo(
        [FromQuery] List<string>? tecnicos, CancellationToken cancellationToken) =>
        notificacoes.ResumoAsync(tecnicos, cancellationToken);

    [HttpGet("tecnicos")]
    [RequerPermissao(ModuloPermissao.RegulacaoEsusSg, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<TecnicoNotificacaoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<TecnicoNotificacaoDto>> Tecnicos(CancellationToken cancellationToken) =>
        notificacoes.TecnicosAsync(cancellationToken);

    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoEsusSg, AcoesPermissao.Consulta)]
    [ProducesResponseType<EsusSgNotificacaoPaginaDto>(StatusCodes.Status200OK)]
    public Task<EsusSgNotificacaoPaginaDto> Listar(
        [FromQuery] EsusSgNotificacaoFiltroDto filtro, CancellationToken cancellationToken) =>
        notificacoes.ListarAsync(filtro, cancellationToken);

    /// <summary>"Visto" é marca NOSSA (tira da fila de notificações), não escrita no ESUS —
    /// por isso basta Consulta: a integração com o ESUS é só leitura nesta entrega.</summary>
    [HttpPost("{id:guid}/vista")]
    [RequerPermissao(ModuloPermissao.RegulacaoEsusSg, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarVista(Guid id, CancellationToken cancellationToken)
    {
        await notificacoes.MarcarVistaAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("solicitacao/{idEsusSg}/vistas")]
    [RequerPermissao(ModuloPermissao.RegulacaoEsusSg, AcoesPermissao.Consulta)]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public Task<int> MarcarVistasDaSolicitacao(string idEsusSg, CancellationToken cancellationToken) =>
        notificacoes.MarcarVistasDaSolicitacaoAsync(idEsusSg, cancellationToken);
}

/// <summary>Motor do ESUS SG: status, rodadas, disparo, credencial, agendamento diário, catálogo.</summary>
[ApiController]
[Route("regulacao/esussg/configuracao")]
public sealed class EsusSgConfiguracaoController(
    IEsusSgMotorService motor,
    IUsuarioAtualAccessor usuarioAtual) : ControllerBase
{
    [HttpGet("status")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<EsusSgStatusMotorDto>(StatusCodes.Status200OK)]
    public Task<EsusSgStatusMotorDto> Status(CancellationToken cancellationToken) =>
        motor.ObterStatusAsync(cancellationToken);

    [HttpGet("execucoes")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<EsusSgExecucaoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<EsusSgExecucaoDto>> Execucoes(
        [FromQuery] int limite = 20, CancellationToken cancellationToken = default) =>
        motor.ListarExecucoesAsync(limite, cancellationToken);

    /// <summary>Enfileira uma rodada (202). A carga inicial lê TODO o histórico de agendados desde
    /// 2015, em fatias de um ano — alguns minutos, e é retomável se o serviço reiniciar.</summary>
    [HttpPost("varreduras")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disparar(
        [FromBody] EsusSgDispararVarreduraDto pedido, CancellationToken cancellationToken)
    {
        await motor.DispararAsync(pedido, usuarioAtual.UsuarioId, User.Identity?.Name, cancellationToken);
        return Accepted();
    }

    [HttpPut("credencial")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SalvarCredencial(
        [FromBody] EsusSgCredencialRequest requisicao, CancellationToken cancellationToken)
    {
        await motor.SalvarCredencialAsync(requisicao, cancellationToken);
        return NoContent();
    }

    /// <summary>Testa o login sem salvar. Devolve o nome do usuário no ESUS (prova de que entrou).</summary>
    [HttpPost("testar-credencial")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<string>> TestarCredencial(
        [FromBody] EsusSgCredencialRequest requisicao, CancellationToken cancellationToken) =>
        Ok(await motor.TestarCredencialAsync(requisicao, cancellationToken));

    [HttpGet("varredura-automatica")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<EsusSgVarreduraConfigDto>(StatusCodes.Status200OK)]
    public Task<EsusSgVarreduraConfigDto> ObterVarreduraAutomatica(
        [FromServices] IEsusSgVarreduraConfigService config, CancellationToken cancellationToken) =>
        config.ObterAsync(cancellationToken);

    [HttpPut("varredura-automatica")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<EsusSgVarreduraConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<EsusSgVarreduraConfigDto> SalvarVarreduraAutomatica(
        [FromBody] EsusSgVarreduraConfigDto corpo,
        [FromServices] IEsusSgVarreduraConfigService config,
        CancellationToken cancellationToken) =>
        config.SalvarAsync(corpo, cancellationToken);

    /// <summary>Relê o catálogo do ESUS agora (1 requisição) e, se mudou, leva ao catálogo canônico.</summary>
    [HttpPost("catalogo/sincronizar")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<EsusSgCatalogoSyncResultado>(StatusCodes.Status200OK)]
    public Task<EsusSgCatalogoSyncResultado> SincronizarCatalogo(
        [FromServices] IEsusSgCatalogoSyncService catalogo, CancellationToken cancellationToken) =>
        catalogo.SincronizarAsync(cancellationToken);
}

/// <summary>Estatísticas de operadores do ESUS SG (quem incluiu na fila, quem agendou).</summary>
[ApiController]
[Route("regulacao/esussg/estatisticas/operadores")]
public sealed class EsusSgEstatisticasController(IEstatisticasOperadoresExternosService estatisticas) : ControllerBase
{
    private const FonteEstatisticaExterna Fonte = FonteEstatisticaExterna.EsusSg;

    [HttpGet("equipe")]
    [RequerPermissao(ModuloPermissao.EstatisticaEsusSg, AcoesPermissao.Consulta)]
    [ProducesResponseType<EquipeExternaEstatisticaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<EquipeExternaEstatisticaDto> Equipe(
        [FromQuery] DateOnly de, [FromQuery] DateOnly ate, CancellationToken cancellationToken = default) =>
        await estatisticas.EquipeAsync(Fonte, de, ate, cancellationToken);

    [HttpGet("individual")]
    [RequerPermissao(ModuloPermissao.EstatisticaEsusSg, AcoesPermissao.Consulta)]
    [ProducesResponseType<IndividualExternoEstatisticaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IndividualExternoEstatisticaDto> Individual(
        [FromQuery] string chave, [FromQuery] DateOnly de, [FromQuery] DateOnly ate,
        CancellationToken cancellationToken = default) =>
        await estatisticas.IndividualAsync(Fonte, chave, de, ate, cancellationToken);

    [HttpGet("configuracao")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<OperadoresExternosConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<OperadoresExternosConfiguracaoDto> Configuracao(CancellationToken cancellationToken = default) =>
        await estatisticas.ConfiguracaoAsync(Fonte, cancellationToken);

    [HttpPut("configuracao")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<OperadoresExternosConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<OperadoresExternosConfiguracaoDto> SalvarConfiguracao(
        [FromBody] SalvarOperadoresExternosHabilitadosRequest request, CancellationToken cancellationToken = default) =>
        await estatisticas.SalvarHabilitadosAsync(Fonte, request.Nomes, cancellationToken);
}

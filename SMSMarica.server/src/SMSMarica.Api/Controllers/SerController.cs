using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Ser;
using SMSMarica.Core.Ser.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Fila do SER (Sistema Estadual de Regulação, SES-RJ) espelhada na nossa base — ADR-0042.
///
/// <para><b>A busca lê o NOSSO banco, não o SER.</b> Consultar o SER ao vivo custaria ~0,7 s por
/// pesquisa, exigiria sessão ativa — que é única por operador e derrubaria o humano logado — e
/// ficaria refém do teto de 100 registros da tela deles. Quem fala com o SER é o motor de
/// varredura, em background.</para>
///
/// <para>Tudo aqui é leitura: o módulo <see cref="ModuloPermissao.RegulacaoSer"/> só usa a ação
/// <c>Consulta</c>. Disparar a varredura fica em <c>SerConfiguracaoController</c>, sob o módulo
/// de configuração da Regulação.</para>
/// </summary>
[ApiController]
[Route("regulacao/ser")]
public sealed class SerController(ISerConsultaService consulta) : ControllerBase
{
    /// <summary>Busca paginada na fila espelhada.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerBuscaResultadoDto>(StatusCodes.Status200OK)]
    public Task<SerBuscaResultadoDto> Buscar(
        [FromQuery] SerBuscaFiltroDto filtro, CancellationToken cancellationToken) =>
        consulta.BuscarAsync(filtro, cancellationToken);

    /// <summary>Contagem por situação — os cards do topo da tela.</summary>
    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SerResumoSituacaoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SerResumoSituacaoDto>> Resumo(CancellationToken cancellationToken) =>
        consulta.ResumoPorSituacaoAsync(cancellationToken);

    /// <summary>Detalhe da solicitação com a trilha de eventos (o "histórico" do SER).</summary>
    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SerSolicitacaoDetalheDto> Obter(Guid id, CancellationToken cancellationToken) =>
        consulta.ObterAsync(id, cancellationToken);
}

/// <summary>
/// Aba SER da Configuração da Regulação: estado do motor, histórico de rodadas e disparo.
/// Usa <see cref="ModuloPermissao.RegulacaoConfiguracao"/> — que já existia — em vez de criar um
/// módulo de configuração paralelo.
/// </summary>
[ApiController]
[Route("regulacao/ser/configuracao")]
public sealed class SerConfiguracaoController(
    ISerMotorService motor,
    IUsuarioAtualAccessor usuarioAtual) : ControllerBase
{
    /// <summary>Estado do motor: credencial, rodada em andamento, totais e a última execução.</summary>
    [HttpGet("status")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerStatusMotorDto>(StatusCodes.Status200OK)]
    public Task<SerStatusMotorDto> Status(CancellationToken cancellationToken) =>
        motor.ObterStatusAsync(cancellationToken);

    /// <summary>Últimas rodadas do motor.</summary>
    [HttpGet("execucoes")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SerExecucaoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SerExecucaoDto>> Execucoes(
        [FromQuery] int limite = 20, CancellationToken cancellationToken = default) =>
        motor.ListarExecucoesAsync(limite, cancellationToken);

    /// <summary>
    /// Enfileira uma varredura. Responde 202 — a rodada leva de 15 min a ~1 h e roda em
    /// background; a tela acompanha por <c>GET status</c>. Responde 409 se já houver uma em
    /// andamento (a sessão do SER é única por operador).
    /// </summary>
    [HttpPost("varreduras")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disparar(
        [FromBody] SerDispararVarreduraDto pedido, CancellationToken cancellationToken)
    {
        // O accessor não expõe nome; o rastreio guarda o e-mail da claim, que é o que identifica
        // o operador na tela de execuções sem precisar de join com `usuario`.
        var nome = User.Identity?.Name;
        await motor.DispararAsync(pedido, usuarioAtual.UsuarioId, nome, cancellationToken);
        return Accepted();
    }

    /// <summary>Testa uma credencial contra o SER sem gravá-la. Só autentica e confere se o
    /// módulo Ambulatório abre — nenhuma escrita no SER.</summary>
    [HttpPost("testar-credencial")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TestarCredencial(
        [FromBody] SerTestarCredencialRequest requisicao, CancellationToken cancellationToken)
    {
        await motor.TestarCredencialAsync(requisicao.Usuario, requisicao.Senha, cancellationToken);
        return NoContent();
    }
}

/// <summary>Credencial avulsa para teste (não é gravada aqui).</summary>
public sealed record SerTestarCredencialRequest(string Usuario, string Senha);

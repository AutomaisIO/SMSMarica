using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMais.Api.Auth;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Sernit;
using SMSMais.Core.Sernit.Dtos;
using SMSMais.Core.Sernit.Sessao;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Fila do SERNIT (SER de Niterói) espelhada na nossa base — subsistema irmão do SER-RJ (ADR-0042).
///
/// <para><b>A busca lê o NOSSO banco, não o SERNIT.</b> Quem fala com o SERNIT é o motor de
/// varredura, em background. Módulo <see cref="ModuloPermissao.RegulacaoSernit"/>: <c>Consulta</c>
/// para ler; disparo/configuração ficam em <c>SernitConfiguracaoController</c>.</para>
/// </summary>
[ApiController]
[Route("regulacao/sernit")]
public sealed class SernitController(ISernitConsultaService consulta) : ControllerBase
{
    /// <summary>Busca paginada na fila espelhada.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitBuscaResultadoDto>(StatusCodes.Status200OK)]
    public Task<SernitBuscaResultadoDto> Buscar(
        [FromQuery] SernitBuscaFiltroDto filtro, CancellationToken cancellationToken) =>
        consulta.BuscarAsync(filtro, cancellationToken);

    /// <summary>Contagem por situação — os cards do topo da tela.</summary>
    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SernitResumoSituacaoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SernitResumoSituacaoDto>> Resumo(CancellationToken cancellationToken) =>
        consulta.ResumoPorSituacaoAsync(cancellationToken);

    /// <summary>Detalhe da solicitação com a trilha de eventos (o "histórico" do SERNIT).</summary>
    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitSolicitacaoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SernitSolicitacaoDetalheDto> Obter(Guid id, CancellationToken cancellationToken) =>
        consulta.ObterAsync(id, cancellationToken);

    /// <summary>Registra um FollowUP na solicitação — <b>escreve no SERNIT</b>. Assinado pelo
    /// operador (exige a sessão de <c>/regulacao/sernit/sessao</c>); só retorna sucesso depois de
    /// RELER o histórico e achar o evento lá.</summary>
    [HttpPost("{id:guid}/followup")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Edicao)]
    [ProducesResponseType<SernitFollowUpResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SernitFollowUpResultadoDto> RegistrarFollowUp(
        Guid id,
        [FromBody] SernitFollowUpRequest corpo,
        [FromServices] ISernitEscritaService escrita,
        CancellationToken cancellationToken) =>
        escrita.RegistrarFollowUpAsync(id, corpo.Texto, cancellationToken);

    /// <summary>Os telefones como o SERNIT os tem AGORA — lidos ao vivo da tela de edição.</summary>
    [HttpGet("{id:guid}/contatos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitContatosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<SernitContatosDto> Contatos(
        Guid id,
        [FromServices] ISernitEscritaService escrita,
        CancellationToken cancellationToken) =>
        escrita.LerContatosAsync(id, cancellationToken);

    /// <summary>Altera os telefones da solicitação <b>no SERNIT</b>. Campo ausente/<c>null</c> não
    /// é tocado; string vazia limpa. Trava invertida + releitura antes de responder. Não altera o
    /// hub FHIR. Exige CPF no cadastro (o SERNIT recusa o Gravar sem CPF).</summary>
    [HttpPut("{id:guid}/contatos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Edicao)]
    [ProducesResponseType<SernitContatosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SernitContatosDto> AlterarContatos(
        Guid id,
        [FromBody] SernitAlterarContatosRequest corpo,
        [FromServices] ISernitEscritaService escrita,
        CancellationToken cancellationToken) =>
        escrita.AlterarContatosAsync(id, corpo, cancellationToken);
}

/// <summary>
/// A sessão de ESCRITA do operador no SERNIT. A credencial cadastrada é de sincronismo (a
/// varredura lê); escrever usa o PRÓPRIO login do operador, porque o SERNIT assina cada evento com
/// o nome de quem fez. A senha não é persistida — vive em memória, amarrada ao <c>jti</c>.
/// </summary>
[ApiController]
[Route("regulacao/sernit/sessao")]
public sealed class SernitSessaoOperadorController(
    ISernitSessaoOperadorStore sessoes,
    IUsuarioAtualAccessor usuarioAtual) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitSessaoOperadorInfo>(StatusCodes.Status200OK)]
    public SernitSessaoOperadorInfo Estado() => sessoes.Estado(Sessao());

    [HttpPost]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Edicao)]
    [ProducesResponseType<SernitSessaoOperadorInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<SernitSessaoOperadorInfo> Entrar(
        [FromBody] SernitLoginOperadorRequest corpo, CancellationToken cancellationToken) =>
        sessoes.AutenticarAsync(Sessao(), Operador(), corpo.Usuario, corpo.Senha, cancellationToken);

    [HttpDelete]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Sair()
    {
        sessoes.Encerrar(Sessao());
        return NoContent();
    }

    private string Sessao() =>
        usuarioAtual.SessaoId
        ?? throw new ValidacaoException(
            "sernit.sem_operador",
            "Escrita no SERNIT exige um usuário autenticado — a ação é assinada por quem a fez.");

    private Guid Operador() =>
        usuarioAtual.UsuarioId
        ?? throw new ValidacaoException(
            "sernit.sem_operador",
            "Escrita no SERNIT exige um usuário autenticado — a ação é assinada por quem a fez.");
}

/// <summary>
/// <b>Regulação → Notificações do SERNIT</b>: o que mudou e ainda ninguém olhou. Primeiro consumidor
/// da fila <c>sernit_gatilho</c>; marcar como visto tira o item da fila.
/// </summary>
[ApiController]
[Route("regulacao/sernit/notificacoes")]
public sealed class SernitNotificacaoController(ISernitNotificacaoService notificacoes) : ControllerBase
{
    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitNotificacaoResumoDto>(StatusCodes.Status200OK)]
    public Task<SernitNotificacaoResumoDto> Resumo(CancellationToken cancellationToken) =>
        notificacoes.ResumoAsync(cancellationToken);

    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitNotificacaoPaginaDto>(StatusCodes.Status200OK)]
    public Task<SernitNotificacaoPaginaDto> Listar(
        [FromQuery] SernitNotificacaoFiltroDto filtro, CancellationToken cancellationToken) =>
        notificacoes.ListarAsync(filtro, cancellationToken);

    [HttpPost("{id:guid}/vista")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarVista(Guid id, CancellationToken cancellationToken)
    {
        await notificacoes.MarcarVistaAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("solicitacao/{idSernit}/vistas")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Edicao)]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public Task<int> MarcarVistasDaSolicitacao(string idSernit, CancellationToken cancellationToken) =>
        notificacoes.MarcarVistasDaSolicitacaoAsync(idSernit, cancellationToken);
}

/// <summary>
/// Alinhamento único da base do SERNIT com o hub FHIR. Fora da tela de propósito — é um acerto de
/// uma vez; depois quem mantém em dia é a varredura. <b>Gate operacional:</b> só com backup do
/// <c>fhir.patient</c> (o hub não tem undo).
/// </summary>
[ApiController]
[Route("regulacao/sernit/pacientes")]
public sealed class SernitPacientesController : ControllerBase
{
    [HttpPost("backfill")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult Backfill(
        [FromServices] IServiceScopeFactory escopos,
        [FromServices] ILogger<SernitPacientesController> logger,
        [FromQuery] int throttleMs = 25)
    {
        _ = Task.Run(async () =>
        {
            using var escopo = escopos.CreateScope();
            try
            {
                var svc = escopo.ServiceProvider
                    .GetRequiredService<SMSMais.Core.Sernit.Pacientes.ISernitBackfillPacientesService>();
                await svc.ExecutarAsync(throttleMs, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SERNIT/backfill de pacientes falhou.");
            }
        });

        return Accepted(new
        {
            mensagem = "Backfill de pacientes do SERNIT iniciado em background. "
                       + "Acompanhe pelos logs do server.",
        });
    }
}

/// <summary>
/// Aba SERNIT da Configuração da Regulação: estado do motor, histórico de rodadas, disparo e
/// credencial. Usa <see cref="ModuloPermissao.RegulacaoConfiguracao"/>, como o SER-RJ.
/// </summary>
[ApiController]
[Route("regulacao/sernit/configuracao")]
public sealed class SernitConfiguracaoController(
    ISernitMotorService motor,
    IUsuarioAtualAccessor usuarioAtual) : ControllerBase
{
    /// <summary>Estado do motor: credencial, rodada em andamento, totais e a última execução.</summary>
    [HttpGet("status")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitStatusMotorDto>(StatusCodes.Status200OK)]
    public Task<SernitStatusMotorDto> Status(CancellationToken cancellationToken) =>
        motor.ObterStatusAsync(cancellationToken);

    /// <summary>Últimas rodadas do motor.</summary>
    [HttpGet("execucoes")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SernitExecucaoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SernitExecucaoDto>> Execucoes(
        [FromQuery] int limite = 20, CancellationToken cancellationToken = default) =>
        motor.ListarExecucoesAsync(limite, cancellationToken);

    /// <summary>Enfileira uma varredura (202). 409 se já houver uma em andamento.</summary>
    [HttpPost("varreduras")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disparar(
        [FromBody] SernitDispararVarreduraDto pedido, CancellationToken cancellationToken)
    {
        var nome = User.Identity?.Name;
        await motor.DispararAsync(pedido, usuarioAtual.UsuarioId, nome, cancellationToken);
        return Accepted();
    }

    /// <summary>Grava a credencial do SERNIT (cifrada). Autentica antes de persistir.</summary>
    [HttpPut("credencial")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SalvarCredencial(
        [FromBody] SernitTestarCredencialRequest requisicao, CancellationToken cancellationToken)
    {
        await motor.SalvarCredencialAsync(requisicao.Usuario, requisicao.Senha, cancellationToken);
        return NoContent();
    }

    /// <summary>Testa uma credencial contra o SERNIT sem gravá-la.</summary>
    [HttpPost("testar-credencial")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TestarCredencial(
        [FromBody] SernitTestarCredencialRequest requisicao, CancellationToken cancellationToken)
    {
        await motor.TestarCredencialAsync(requisicao.Usuario, requisicao.Senha, cancellationToken);
        return NoContent();
    }

    /// <summary>Configuração do disparo diário (ligado/desligado + hora de Brasília).</summary>
    [HttpGet("varredura-automatica")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitVarreduraConfigDto>(StatusCodes.Status200OK)]
    public Task<SernitVarreduraConfigDto> ObterVarreduraAutomatica(
        [FromServices] ISernitVarreduraConfigService config, CancellationToken cancellationToken) =>
        config.ObterAsync(cancellationToken);

    [HttpPut("varredura-automatica")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<SernitVarreduraConfigDto>(StatusCodes.Status200OK)]
    public Task<SernitVarreduraConfigDto> SalvarVarreduraAutomatica(
        [FromBody] SernitVarreduraConfigDto corpo,
        [FromServices] ISernitVarreduraConfigService config,
        CancellationToken cancellationToken) =>
        config.SalvarAsync(corpo, cancellationToken);

    /// <summary>Copia o catálogo do SERNIT para a nossa base (leitura longa, retomável, background).</summary>
    [HttpPost("catalogo/sincronizar")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult SincronizarCatalogo(
        [FromQuery] bool refazerTudo,
        [FromServices] SMSMais.Core.Sernit.Background.ISernitCatalogoSyncFila fila)
    {
        if (!fila.TentarEnfileirar(refazerTudo))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conflito",
                Detail = "A cópia do catálogo do SERNIT já está em andamento.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        return Accepted();
    }

    /// <summary>Formulário de NOVA solicitação, lido ao vivo do SERNIT. Somente leitura.</summary>
    [HttpGet("nova-solicitacao/formulario")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitFormularioNovaDto>(StatusCodes.Status200OK)]
    public Task<SernitFormularioNovaDto> FormularioNovaSolicitacao(
        [FromServices] ISernitNovaSolicitacaoService nova, CancellationToken cancellationToken) =>
        nova.ObterFormularioAsync(cancellationToken);

    [HttpGet("nova-solicitacao/recursos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SernitOpcaoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SernitOpcaoDto>> RecursosNovaSolicitacao(
        [FromQuery] string tipo,
        [FromServices] ISernitNovaSolicitacaoService nova,
        CancellationToken cancellationToken) =>
        nova.ListarRecursosAsync(tipo, cancellationToken);

    [HttpGet("nova-solicitacao/campos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SernitCampoDinamicoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SernitCampoDinamicoDto>> CamposNovaSolicitacao(
        [FromQuery] string tipo,
        [FromQuery] string recurso,
        [FromServices] ISernitNovaSolicitacaoService nova,
        CancellationToken cancellationToken) =>
        nova.ObterCamposDinamicosAsync(tipo, recurso, cancellationToken);

    /// <summary>Pesquisa o paciente no SERNIT por CNS ou CPF (o motor do próprio SERNIT). Consulta.</summary>
    [HttpGet("nova-solicitacao/paciente")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitPacienteEncontradoDto>(StatusCodes.Status200OK)]
    public Task<SernitPacienteEncontradoDto> PesquisarPacienteNoSernit(
        [FromQuery] string documento,
        [FromServices] ISernitNovaSolicitacaoService nova,
        CancellationToken cancellationToken) =>
        nova.PesquisarPacienteAsync(documento, cancellationToken);

    /// <summary>CID que o SERNIT aceita como Hipótese: do espelho quando existe, ao vivo enquanto não.</summary>
    [HttpGet("nova-solicitacao/cids")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitCidSugestoesDto>(StatusCodes.Status200OK)]
    public async Task<SernitCidSugestoesDto> CidsNovaSolicitacao(
        [FromQuery] string tipo,
        [FromQuery] string recurso,
        [FromQuery] string? termo,
        [FromServices] ISernitCatalogoService catalogo,
        [FromServices] ISernitNovaSolicitacaoService nova,
        CancellationToken cancellationToken)
    {
        var doTipo = string.Equals(tipo, "EXAME", StringComparison.OrdinalIgnoreCase)
            ? Data.Entities.Sernit.TipoRecursoSernit.Exame
            : Data.Entities.Sernit.TipoRecursoSernit.Consulta;

        var busca = termo ?? string.Empty;
        var espelho = await catalogo.BuscarCidsAsync(doTipo, recurso, busca, cancellationToken);
        return espelho ?? await nova.SugerirCidsAsync(tipo, recurso, busca, cancellationToken);
    }
}

/// <summary>
/// <b>Regulação → Nova solicitação (SERNIT)</b>: catálogo espelhado e rascunhos. Tudo LOCAL — o
/// envio ao SERNIT é um passo separado ainda não ligado.
/// </summary>
[ApiController]
[Route("regulacao/sernit/rascunhos")]
public sealed class SernitRascunhoController(
    ISernitCatalogoService catalogo,
    ISernitRascunhoService rascunhos) : ControllerBase
{
    [HttpGet("formulario")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitCatalogoFormularioDto>(StatusCodes.Status200OK)]
    public Task<SernitCatalogoFormularioDto> Formulario(CancellationToken cancellationToken) =>
        catalogo.ObterFormularioAsync(cancellationToken);

    [HttpGet("campos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SernitCampoDinamicoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SernitCampoDinamicoDto>> Campos(
        [FromQuery] Data.Entities.Sernit.TipoRecursoSernit tipo,
        [FromQuery] string recurso,
        CancellationToken cancellationToken) =>
        catalogo.ObterCamposAsync(tipo, recurso, cancellationToken);

    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SernitRascunhoListaDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SernitRascunhoListaDto>> Listar(
        [FromQuery] Data.Entities.Sernit.StatusRascunhoSernit? status, CancellationToken cancellationToken) =>
        rascunhos.ListarAsync(status, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Consulta)]
    [ProducesResponseType<SernitRascunhoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SernitRascunhoDetalheDto> Obter(Guid id, CancellationToken cancellationToken) =>
        rascunhos.ObterAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Edicao)]
    [ProducesResponseType<SernitRascunhoDetalheDto>(StatusCodes.Status200OK)]
    public Task<SernitRascunhoDetalheDto> Criar(
        [FromBody] SernitRascunhoRequest corpo, CancellationToken cancellationToken) =>
        rascunhos.SalvarAsync(null, corpo, cancellationToken);

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Edicao)]
    [ProducesResponseType<SernitRascunhoDetalheDto>(StatusCodes.Status200OK)]
    public Task<SernitRascunhoDetalheDto> Salvar(
        Guid id, [FromBody] SernitRascunhoRequest corpo, CancellationToken cancellationToken) =>
        rascunhos.SalvarAsync(id, corpo, cancellationToken);

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        await rascunhos.ExcluirAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/pronto")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Edicao)]
    [ProducesResponseType<SernitRascunhoDetalheDto>(StatusCodes.Status200OK)]
    public Task<SernitRascunhoDetalheDto> MarcarPronto(Guid id, CancellationToken cancellationToken) =>
        rascunhos.MarcarProntoAsync(id, cancellationToken);

    [HttpPost("{id:guid}/anexos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Edicao)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType<SernitRascunhoAnexoDto>(StatusCodes.Status200OK)]
    public async Task<SernitRascunhoAnexoDto> Anexar(
        Guid id, IFormFile arquivo, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, cancellationToken);
        return await rascunhos.AnexarAsync(
            id, arquivo.FileName, arquivo.ContentType, ms.ToArray(), cancellationToken);
    }

    [HttpDelete("{id:guid}/anexos/{anexoId:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoSernit, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoverAnexo(
        Guid id, Guid anexoId, CancellationToken cancellationToken)
    {
        await rascunhos.RemoverAnexoAsync(id, anexoId, cancellationToken);
        return NoContent();
    }
}

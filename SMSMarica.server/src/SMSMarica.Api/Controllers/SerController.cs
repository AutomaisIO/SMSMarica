using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Ser;
using SMSMarica.Core.Ser.Dtos;
using SMSMarica.Core.Ser.Sessao;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ser;

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

    /// <summary>
    /// Registra um FollowUP na solicitação — <b>escreve no SER</b> (docs/ser.md §9).
    ///
    /// <para>Assinado pelo operador: exige a sessão de <c>/regulacao/ser/sessao</c>. Sem ela
    /// responde 400 com o código <c>ser.sessao_operador_ausente</c>, que é o sinal para a tela
    /// pedir a senha do SER.</para>
    ///
    /// <para>Só retorna sucesso depois de RELER o histórico e achar o evento lá — o SER já
    /// respondeu "salvo com sucesso" sem ter gravado nada (10/08/2026).</para>
    /// </summary>
    [HttpPost("{id:guid}/followup")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType<SerFollowUpResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SerFollowUpResultadoDto> RegistrarFollowUp(
        Guid id,
        [FromBody] SerFollowUpRequest corpo,
        [FromServices] ISerEscritaService escrita,
        CancellationToken cancellationToken) =>
        escrita.RegistrarFollowUpAsync(id, corpo.Texto, cancellationToken);

    /// <summary>
    /// Os três telefones como o SER os tem AGORA — lidos ao vivo da tela de edição.
    ///
    /// <para>Lê do SER e não do espelho de propósito: o espelho só se atualiza na varredura, e
    /// deixar o operador editar em cima de número velho sobrescreveria o que o Estado já tem.</para>
    /// </summary>
    [HttpGet("{id:guid}/contatos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerContatosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<SerContatosDto> Contatos(
        Guid id,
        [FromServices] ISerEscritaService escrita,
        CancellationToken cancellationToken) =>
        escrita.LerContatosAsync(id, cancellationToken);

    /// <summary>
    /// Altera os telefones da solicitação <b>no SER</b> (docs/ser.md §10).
    ///
    /// <para>Campo ausente/<c>null</c> não é tocado; string vazia limpa. Uma trava invertida
    /// garante que o POST não mexa em nada além dos telefones, e o resultado é RELIDO da tela
    /// antes de responder.</para>
    ///
    /// <para><b>Não altera o nosso hub FHIR</b> — o cadastro clínico do cidadão tem regras
    /// próprias (telefone verificado, contato que só acumula) e não passa por aqui.</para>
    /// </summary>
    [HttpPut("{id:guid}/contatos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType<SerContatosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SerContatosDto> AlterarContatos(
        Guid id,
        [FromBody] SerAlterarContatosRequest corpo,
        [FromServices] ISerEscritaService escrita,
        CancellationToken cancellationToken) =>
        escrita.AlterarContatosAsync(id, corpo, cancellationToken);
}

/// <summary>
/// A sessão de ESCRITA do operador no SER.
///
/// <para><b>Por que não basta a credencial cadastrada.</b> Aquela é de sincronismo — serve para a
/// varredura ler. O SER assina cada evento com o nome de quem fez, então escrever com ela faria
/// toda ação do município aparecer no nome da mesma pessoa na trilha do Estado. Quem escreve usa
/// o PRÓPRIO login do SER.</para>
///
/// <para><b>A senha não é persistida.</b> Vive na memória do processo, amarrada ao usuário, e
/// morre com a sessão dele (ou com o restart da API). Não existe tabela para ela.</para>
/// </summary>
[ApiController]
[Route("regulacao/ser/sessao")]
public sealed class SerSessaoOperadorController(
    ISerSessaoOperadorStore sessoes,
    IUsuarioAtualAccessor usuarioAtual) : ControllerBase
{
    /// <summary>A tela pergunta isto antes de oferecer as ações de escrita.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerSessaoOperadorInfo>(StatusCodes.Status200OK)]
    public SerSessaoOperadorInfo Estado() => sessoes.Estado(Sessao());

    /// <summary>Entra no SER com a credencial do operador. Valida CONTRA O SER na hora.</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType<SerSessaoOperadorInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<SerSessaoOperadorInfo> Entrar(
        [FromBody] SerLoginOperadorRequest corpo, CancellationToken cancellationToken) =>
        sessoes.AutenticarAsync(Sessao(), Operador(), corpo.Usuario, corpo.Senha, cancellationToken);

    /// <summary>Sai do SER. O front chama isto no logout — sair daqui é sair de lá.</summary>
    [HttpDelete]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Sair()
    {
        sessoes.Encerrar(Sessao());
        return NoContent();
    }

    /// <summary>A SESSÃO (jti), não o usuário: é o que faz sair-e-entrar começar do zero.</summary>
    private string Sessao() =>
        usuarioAtual.SessaoId
        ?? throw new SMSMarica.Core.Common.Excecoes.ValidacaoException(
            "ser.sem_operador",
            "Escrita no SER exige um usuário autenticado — a ação é assinada por quem a fez.");

    private Guid Operador() =>
        usuarioAtual.UsuarioId
        ?? throw new SMSMarica.Core.Common.Excecoes.ValidacaoException(
            "ser.sem_operador",
            "Escrita no SER exige um usuário autenticado — a ação é assinada por quem a fez.");
}

/// <summary>
/// <b>Regulação → Notificações</b>: o que mudou no SER e ainda ninguém olhou.
///
/// <para>É o primeiro consumidor da fila de gatilhos. Marcar como visto carimba
/// <c>processado_em</c>/<c>processado_por</c>, que é o que tira o item da fila — então a tela
/// serve, ao mesmo tempo, para operar e para validar se o motor está gerando gatilho certo.</para>
/// </summary>
[ApiController]
[Route("regulacao/ser/notificacoes")]
public sealed class SerNotificacaoController(ISerNotificacaoService notificacoes) : ControllerBase
{
    /// <summary>Contadores por tipo de recurso e situação — as abas e os números.</summary>
    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerNotificacaoResumoDto>(StatusCodes.Status200OK)]
    public Task<SerNotificacaoResumoDto> Resumo(CancellationToken cancellationToken) =>
        notificacoes.ResumoAsync(cancellationToken);

    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerNotificacaoPaginaDto>(StatusCodes.Status200OK)]
    public Task<SerNotificacaoPaginaDto> Listar(
        [FromQuery] SerNotificacaoFiltroDto filtro, CancellationToken cancellationToken) =>
        notificacoes.ListarAsync(filtro, cancellationToken);

    /// <summary>Marca UM movimento como visto.</summary>
    [HttpPost("{id:guid}/vista")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarVista(Guid id, CancellationToken cancellationToken)
    {
        await notificacoes.MarcarVistaAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Marca tudo que está pendente de UMA solicitação — quem abriu a solicitação viu
    /// todas as movimentações dela.</summary>
    [HttpPost("solicitacao/{idSer}/vistas")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public Task<int> MarcarVistasDaSolicitacao(string idSer, CancellationToken cancellationToken) =>
        notificacoes.MarcarVistasDaSolicitacaoAsync(idSer, cancellationToken);
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
    ISerConsultaDiretaService consultaDireta,
    IUsuarioAtualAccessor usuarioAtual) : ControllerBase
{
    /// <summary>
    /// Consulta DIRETA ao SER — mesmos filtros da tela de lá, resultado cru, <b>nada gravado</b>.
    /// É o ensaio que valida o motor inteiro (login, AJAXREQUEST, ViewState, busca, paginação e
    /// parser) em segundos, antes de confiar numa varredura de uma hora.
    ///
    /// <para>Cuidado: derruba a sessão de quem estiver logado no SER com essa credencial.</para>
    /// </summary>
    [HttpPost("consulta-direta")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerConsultaDiretaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<SerConsultaDiretaDto> ConsultaDireta(
        [FromBody] SerConsultaDiretaRequest requisicao, CancellationToken cancellationToken) =>
        consultaDireta.ConsultarAsync(requisicao, cancellationToken);

    /// <summary>Histórico lido AO VIVO no SER, para conferir contra a tela de lá.</summary>
    [HttpGet("consulta-direta/{idSer}/historico")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerHistoricoDiretoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<SerHistoricoDiretoDto> HistoricoDireto(
        string idSer, [FromQuery] SituacaoSer situacao, CancellationToken cancellationToken) =>
        consultaDireta.HistoricoAsync(idSer, situacao, cancellationToken);

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

    /// <summary>
    /// Grava a credencial do SER (cifrada no store de integrações). Autentica antes de
    /// persistir — só salva o que o SER aceitou.
    /// </summary>
    [HttpPut("credencial")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SalvarCredencial(
        [FromBody] SerTestarCredencialRequest requisicao, CancellationToken cancellationToken)
    {
        await motor.SalvarCredencialAsync(requisicao.Usuario, requisicao.Senha, cancellationToken);
        return NoContent();
    }

    /// <summary>Testa uma credencial contra o SER sem gravá-la. Só autentica e confere se o
    /// módulo Ambulatório abre — nenhuma escrita no SER.</summary>
    /// <summary>
    /// Formulário de NOVA solicitação, lido ao vivo da aba Editar do SER.
    ///
    /// <para><b>Somente leitura.</b> Abrir a aba e trocar combos apenas re-renderiza a view do
    /// SER; nada é criado. O envio ainda não existe — quando existir, será endpoint próprio e
    /// decisão explícita.</para>
    /// </summary>
    [HttpGet("nova-solicitacao/formulario")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerFormularioNovaDto>(StatusCodes.Status200OK)]
    public Task<SerFormularioNovaDto> FormularioNovaSolicitacao(
        [FromServices] ISerNovaSolicitacaoService nova, CancellationToken cancellationToken) =>
        nova.ObterFormularioAsync(cancellationToken);

    [HttpGet("nova-solicitacao/recursos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SerOpcaoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SerOpcaoDto>> RecursosNovaSolicitacao(
        [FromQuery] string tipo,
        [FromQuery] bool ambulatorioEstadual,
        [FromServices] ISerNovaSolicitacaoService nova,
        CancellationToken cancellationToken) =>
        nova.ListarRecursosAsync(tipo, ambulatorioEstadual, cancellationToken);

    /// <summary>
    /// Pesquisa o paciente no SER por CNS ou CPF — o motor do próprio SER, que resolve o cadastro
    /// sem depender do CADSUS via SISREG (limitado). Consulta: nada é gravado no SER.
    /// </summary>
    [HttpGet("nova-solicitacao/paciente")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerPacienteEncontradoDto>(StatusCodes.Status200OK)]
    public async Task<SerPacienteEncontradoDto> PesquisarPacienteNoSer(
        [FromQuery] string documento,
        [FromServices] ISerNovaSolicitacaoService nova,
        [FromServices] Core.Pacientes.IPacientesService pacientes,
        [FromServices] Core.Pacientes.Fhir.IPacienteResolver resolver,
        CancellationToken cancellationToken)
    {
        var doSer = await nova.PesquisarPacienteAsync(documento, cancellationToken);
        if (!doSer.Encontrado) return doSer;

        // Cruza com o NOSSO cadastro pelo CPF que o SER devolveu — é a chave que une as bases
        // (o CNS não serve: a mesma pessoa pode ter mais de um, como o piloto de conciliação
        // mostrou). Falha aqui não pode derrubar a pesquisa: o dado do SER já é útil sozinho.
        try
        {
            var cpf = doSer.Campos.FirstOrDefault(c => c.Campo.EndsWith(":cpf", StringComparison.Ordinal))?.Valor;
            if (string.IsNullOrWhiteSpace(cpf)) return doSer;

            if (await pacientes.ObterPorCpfAsync(cpf, cancellationToken) is not { } nosso) return doSer;
            var resumo = await resolver.ResolverAsync(nosso.Id, cancellationToken);

            return doSer with
            {
                PacienteIdNosso = nosso.Id,
                TelefoneVerificadoNosso = resumo?.TelefoneVerificado,
            };
        }
        catch (Exception)
        {
            return doSer;
        }
    }

    [HttpGet("nova-solicitacao/campos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SerCampoDinamicoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SerCampoDinamicoDto>> CamposNovaSolicitacao(
        [FromQuery] string tipo,
        [FromQuery] string recurso,
        [FromQuery] bool ambulatorioEstadual,
        [FromServices] ISerNovaSolicitacaoService nova,
        CancellationToken cancellationToken) =>
        nova.ObterCamposDinamicosAsync(tipo, recurso, ambulatorioEstadual, cancellationToken);

    /// <summary>
    /// Lista os CID que o SER aceita como <b>Hipótese</b> para aquele recurso.
    ///
    /// <para><b>Do espelho quando ele existe; ao vivo enquanto não existe.</b> A lista é do
    /// RECURSO — o oncológico só aceita neoplasia, a cardiologia aceita o CID-10 inteiro —, então
    /// não há uma lista única a servir. O espelho guarda a relação de cada uma delas, copiada do
    /// próprio SER; recurso ainda não copiado cai no autocomplete ao vivo, que é a fonte.</para>
    ///
    /// <para>Consulta nos dois caminhos: no ao vivo é só o fetch de sugestões, nada é gravado.</para>
    /// </summary>
    [HttpGet("nova-solicitacao/cids")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerCidSugestoesDto>(StatusCodes.Status200OK)]
    public async Task<SerCidSugestoesDto> CidsNovaSolicitacao(
        [FromQuery] string tipo,
        [FromQuery] string recurso,
        [FromQuery] bool ambulatorioEstadual,
        [FromQuery] string termo,
        [FromServices] ISerCatalogoService catalogo,
        [FromServices] ISerNovaSolicitacaoService nova,
        CancellationToken cancellationToken)
    {
        var doTipo = string.Equals(tipo, "EXAME", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(tipo, "Exame", StringComparison.OrdinalIgnoreCase)
            ? TipoRecursoSer.Exame
            : TipoRecursoSer.Consulta;

        var espelho = await catalogo.BuscarCidsAsync(
            doTipo, recurso, ambulatorioEstadual, termo, cancellationToken);

        return espelho ?? await nova.SugerirCidsAsync(
            tipo, recurso, ambulatorioEstadual, termo, cancellationToken);
    }

    /// <summary>Copia o catálogo do SER para a nossa base. Leitura longa (~15 min, uma ida por
    /// recurso) e retomável — recurso já lido não é pedido de novo, salvo `refazerTudo`.</summary>
    [HttpPost("catalogo/sincronizar")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult SincronizarCatalogo(
        [FromQuery] bool refazerTudo,
        [FromServices] SMSMarica.Core.Ser.Background.ISerCatalogoSyncFila fila)
    {
        // Responde na hora: a cópia roda em segundo plano e a tela acompanha pelo progresso.
        if (!fila.TentarEnfileirar(refazerTudo))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conflito",
                Detail = "A cópia do catálogo já está em andamento.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        return Accepted();
    }

    /// <summary>Configuração do disparo diário (ligado/desligado + hora de Brasília).</summary>
    [HttpGet("varredura-automatica")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerVarreduraConfigDto>(StatusCodes.Status200OK)]
    public Task<SerVarreduraConfigDto> ObterVarreduraAutomatica(
        [FromServices] ISerVarreduraConfigService config, CancellationToken cancellationToken) =>
        config.ObterAsync(cancellationToken);

    [HttpPut("varredura-automatica")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<SerVarreduraConfigDto>(StatusCodes.Status200OK)]
    public Task<SerVarreduraConfigDto> SalvarVarreduraAutomatica(
        [FromBody] SerVarreduraConfigDto corpo,
        [FromServices] ISerVarreduraConfigService config,
        CancellationToken cancellationToken) =>
        config.SalvarAsync(corpo, cancellationToken);

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


/// <summary>
/// <b>Regulação → Nova solicitação</b>: catálogo espelhado e rascunhos.
///
/// <para>Tudo aqui é LOCAL — nenhuma requisição ao SER. O formulário vem do catálogo copiado, o
/// pedido é montado e guardado aqui com os anexos, e o envio ao SER é um passo separado que ainda
/// não está ligado.</para>
/// </summary>
[ApiController]
[Route("regulacao/ser/rascunhos")]
public sealed class SerRascunhoController(
    ISerCatalogoService catalogo,
    ISerRascunhoService rascunhos) : ControllerBase
{
    /// <summary>Formulário montado do NOSSO catálogo — offline e instantâneo.</summary>
    [HttpGet("formulario")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerCatalogoFormularioDto>(StatusCodes.Status200OK)]
    public Task<SerCatalogoFormularioDto> Formulario(CancellationToken cancellationToken) =>
        catalogo.ObterFormularioAsync(cancellationToken);

    [HttpGet("campos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SerCampoDinamicoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SerCampoDinamicoDto>> Campos(
        [FromQuery] TipoRecursoSer tipo,
        [FromQuery] string recurso,
        [FromQuery] bool ambulatorioEstadual,
        CancellationToken cancellationToken) =>
        catalogo.ObterCamposAsync(tipo, recurso, ambulatorioEstadual, cancellationToken);

    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SerRascunhoListaDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SerRascunhoListaDto>> Listar(
        [FromQuery] StatusRascunhoSer? status, CancellationToken cancellationToken) =>
        rascunhos.ListarAsync(status, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerRascunhoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SerRascunhoDetalheDto> Obter(Guid id, CancellationToken cancellationToken) =>
        rascunhos.ObterAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType<SerRascunhoDetalheDto>(StatusCodes.Status200OK)]
    public Task<SerRascunhoDetalheDto> Criar(
        [FromBody] SerRascunhoRequest corpo, CancellationToken cancellationToken) =>
        rascunhos.SalvarAsync(null, corpo, cancellationToken);

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType<SerRascunhoDetalheDto>(StatusCodes.Status200OK)]
    public Task<SerRascunhoDetalheDto> Salvar(
        Guid id, [FromBody] SerRascunhoRequest corpo, CancellationToken cancellationToken) =>
        rascunhos.SalvarAsync(id, corpo, cancellationToken);

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        await rascunhos.ExcluirAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Marca como pronto para envio. Recusa se faltar campo obrigatório.</summary>
    [HttpPost("{id:guid}/pronto")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType<SerRascunhoDetalheDto>(StatusCodes.Status200OK)]
    public Task<SerRascunhoDetalheDto> MarcarPronto(Guid id, CancellationToken cancellationToken) =>
        rascunhos.MarcarProntoAsync(id, cancellationToken);

    /// <summary>Anexa um arquivo ao rascunho. Fica guardado AQUI; sobe para o SER só no envio.</summary>
    [HttpPost("{id:guid}/anexos")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType<SerRascunhoAnexoDto>(StatusCodes.Status200OK)]
    public async Task<SerRascunhoAnexoDto> Anexar(
        Guid id, IFormFile arquivo, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, cancellationToken);
        return await rascunhos.AnexarAsync(
            id, arquivo.FileName, arquivo.ContentType, ms.ToArray(), cancellationToken);
    }

    [HttpDelete("{id:guid}/anexos/{anexoId:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoverAnexo(
        Guid id, Guid anexoId, CancellationToken cancellationToken)
    {
        await rascunhos.RemoverAnexoAsync(id, anexoId, cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// Alinhamento único da base do SER com o hub FHIR. <b>Fora da tela de propósito</b> — é um
/// acerto de uma vez, não uma operação de rotina; depois dele quem mantém em dia é a varredura.
/// </summary>
[ApiController]
[Route("regulacao/ser/pacientes")]
public sealed class SerPacientesController : ControllerBase
{
    /// <summary>
    /// Concilia com o hub FHIR os pacientes que o SER já nos deu: cria quem não existe e completa
    /// quem existe com o que faltava (CPF/CNS, nascimento, mãe, endereço, telefones). Nunca
    /// sobrescreve — fonte parcial só acrescenta — e telefone verificado é intocável.
    ///
    /// <para>Roda em BACKGROUND (202); acompanhe pelos logs do server. <b>GATE OPERACIONAL:</b> só
    /// com backup do <c>fhir.patient</c> — o hub não tem undo. Idempotente: rodar de novo depois
    /// de uma queda custa releitura, nunca duplicata.</para>
    /// </summary>
    [HttpPost("backfill")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult Backfill(
        [FromServices] IServiceScopeFactory escopos,
        [FromServices] ILogger<SerPacientesController> logger,
        [FromQuery] int throttleMs = 25)
    {
        // Fire-and-forget em escopo próprio: a requisição responde 202 na hora e o trabalho
        // sobrevive ao fim da conexão. Mesmo padrão do backfill de promoção blob→nativo.
        _ = Task.Run(async () =>
        {
            using var escopo = escopos.CreateScope();
            try
            {
                var svc = escopo.ServiceProvider
                    .GetRequiredService<Core.Ser.Pacientes.ISerBackfillPacientesService>();
                await svc.ExecutarAsync(throttleMs, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SER/backfill de pacientes falhou.");
            }
        });

        return Accepted(new
        {
            mensagem = "Backfill de pacientes do SER iniciado em background. "
                       + "Acompanhe pelos logs do server.",
        });
    }
}

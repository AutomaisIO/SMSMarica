using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMais.Api.Auth;
using SMSMais.Core.Atendimentos;
using SMSMais.Core.Atendimentos.Dtos;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Auditoria.Dtos;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Cidadao.Dtos;
using SMSMais.Core.Conversas;
using SMSMais.Core.Conversas.Dtos;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Agendamentos;
using SMSMais.Core.Pacientes.Agendamentos.Dtos;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

[ApiController]
[Route("pacientes")]
public sealed class PacientesController(
    IPacientesService service,
    IAtendimentosService atendimentos,
    ICidadaoSessaoService sessoes,
    IAuditoriaService auditoria,
    IAgendamentosPacienteService agendamentos,
    IConversasDoPacienteService conversas) : ControllerBase
{
    private readonly IPacientesService _service = service;
    private readonly IAtendimentosService _atendimentos = atendimentos;
    private readonly ICidadaoSessaoService _sessoes = sessoes;
    private readonly IAuditoriaService _auditoria = auditoria;
    private readonly IAgendamentosPacienteService _agendamentos = agendamentos;
    private readonly IConversasDoPacienteService _conversas = conversas;

    /// <summary>
    /// Busca em tempo real por nome (qualquer parte, múltiplos tokens) ou CPF.
    /// Sem <c>termo</c> retorna lista vazia (a base é grande). Limite 20.
    /// </summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<PacienteListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PacienteListItemDto>> Buscar(
        [FromQuery] string? termo,
        CancellationToken cancellationToken) =>
        await _service.BuscarAsync(termo, cancellationToken);

    /// <summary>Retorna um paciente pelo identificador.</summary>
    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<PacienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<PacienteDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    /// <summary>
    /// Histórico clínico do paciente (atendimentos + diagnósticos) vindo do hub
    /// FHIR (Encounter/Condition), originado do Salux. Timeline, mais recente primeiro.
    /// </summary>
    [HttpGet("{id:guid}/atendimentos")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AtendimentoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AtendimentoDto>> Atendimentos(Guid id, CancellationToken cancellationToken) =>
        await _atendimentos.ObterPorPacienteAsync(id, cancellationToken);

    /// <summary>
    /// Agendamentos do paciente (consultas e exames), separados em próximos e histórico, com a
    /// situação normalizada entre as fontes: SER (regulação estadual), SISREG (regulação
    /// municipal) e a agenda própria do município. Exibido na aba "Agendamentos" do cadastro.
    ///
    /// <para>Agregação em LEITURA (sem tabela nova): cada fonte já é materializada localmente pela
    /// sua própria varredura. Gate por Pacientes.Consulta — igual às demais abas do cadastro —,
    /// para que quem abre a ficha veja os agendamentos sem depender do módulo RegulacaoSer.</para>
    /// </summary>
    [HttpGet("{id:guid}/agendamentos")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<AgendamentosPacienteDto>(StatusCodes.Status200OK)]
    public async Task<AgendamentosPacienteDto> Agendamentos(Guid id, CancellationToken cancellationToken) =>
        await _agendamentos.ListarPorPacienteAsync(id, cancellationToken);

    /// <summary>
    /// Histórico de acessos do paciente ao app (sessões de login), mais recentes primeiro.
    /// Exibido na aba "Histórico de Acesso" do cadastro do paciente.
    /// </summary>
    [HttpGet("{id:guid}/acessos")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AcessoCidadaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AcessoCidadaoDto>> Acessos(Guid id, CancellationToken cancellationToken) =>
        await _sessoes.ListarAcessosAsync(id, cancellationToken);

    /// <summary>
    /// Sessões de conversa de WhatsApp do paciente (blocos separados por 24h+ de silêncio),
    /// mais recentes primeiro — mensagens vinculadas a ele + as dos telefones do cadastro.
    /// Aba "Conversas" da ficha; gate por Pacientes.Consulta como as demais abas (o escopo por
    /// unidade/posse é da Central, não da ficha).
    /// </summary>
    [HttpGet("{id:guid}/conversas/sessoes")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SessaoConversaPacienteDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<SessaoConversaPacienteDto>> ConversasSessoes(
        Guid id, CancellationToken cancellationToken) =>
        await _conversas.ListarSessoesAsync(id, cancellationToken);

    /// <summary>Mensagens de uma sessão (telefone + faixa vindos da listagem), em ordem cronológica.</summary>
    [HttpGet("{id:guid}/conversas/mensagens")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<MensagemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<MensagemDto>> ConversasMensagens(
        Guid id,
        [FromQuery] string telefone,
        [FromQuery] DateTime de,
        [FromQuery] DateTime ate,
        CancellationToken cancellationToken) =>
        await _conversas.ObterMensagensDaSessaoAsync(id, telefone, de, ate, cancellationToken);

    /// <summary>
    /// BOTÃO DE PÂNICO: expira TODOS os magic links válidos e revoga TODAS as sessões de
    /// cidadão. Use quando um lote de mensagens pode ter ido para números errados — nenhum
    /// link antigo autentica mais e quem estiver logado cai. O paciente certo reentra pelo
    /// link novo (reenvio) ou pelo OTP.
    /// </summary>
    [HttpPost("acessos/revogar-todos")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Exclusao)]
    [ProducesResponseType<RevogacaoGlobalDto>(StatusCodes.Status200OK)]
    public async Task<RevogacaoGlobalDto> RevogarTodosAcessos(
        [FromBody] RevogarAcessosRequest request, CancellationToken cancellationToken)
    {
        var (links, sessoes) = await _sessoes.RevogarTodosAcessosAsync(
            request.Motivo, cancellationToken);
        return new RevogacaoGlobalDto(links, sessoes);
    }

    /// <summary>
    /// Verifica se há paciente com o CPF informado (inclusive desativado).
    /// 404 se não existe; 200 com o resumo (incluindo <c>ativo</c>) se existe.
    /// </summary>
    [HttpGet("por-cpf/{cpf}")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<PacienteExistenciaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorCpf(string cpf, CancellationToken cancellationToken)
    {
        var resultado = await _service.ObterPorCpfAsync(cpf, cancellationToken);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    /// <summary>
    /// Busca um paciente pelo telefone (Patient.telecom). 404 se nenhum; 200 com o
    /// resumo (id, nome, cpf) do primeiro match. Usado pelo agente de voz (CentralIA)
    /// pra reconhecer quem liga de um número já cadastrado.
    /// </summary>
    [HttpGet("por-telefone/{telefone}")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<PacienteExistenciaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorTelefone(string telefone, CancellationToken cancellationToken)
    {
        var resultado = await _service.ObterPorTelefoneAsync(telefone, cancellationToken);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    /// <summary>Cadastra um novo paciente.</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarPacienteRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    /// <summary>
    /// Promove um Usuario existente (sem papel) a Paciente — usado quando o
    /// fluxo de cadastro detectou que o CPF já existe como usuário.
    /// </summary>
    [HttpPost("promover")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Promover(
        [FromBody] PromoverPacienteRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.PromoverAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    /// <summary>Atualiza dados de um paciente existente.</summary>
    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarPacienteRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Corrige o nome oficial do paciente (fluxo "Verificar nome": recheca o CPF
    /// no motor de busca e permite aplicar o nome retornado ou um ajuste manual).
    /// O nome é normalmente imutável — este é o único ponto que o altera, e a
    /// mudança fica registrada na trilha de auditoria.
    /// </summary>
    [HttpPut("{id:guid}/nome")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarNome(
        Guid id,
        [FromBody] AtualizarNomePacienteRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarNomeAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Histórico de alterações auditadas deste paciente (ex.: correções de nome),
    /// mais recentes primeiro. Gate por Pacientes.Consulta (mesmo do cadastro), para
    /// que quem abre a ficha veja o histórico sem depender do módulo Auditoria.
    /// </summary>
    [HttpGet("{id:guid}/auditoria")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaAuditoriaDto>(StatusCodes.Status200OK)]
    public async Task<PaginaAuditoriaDto> Auditoria(Guid id, CancellationToken cancellationToken) =>
        await _auditoria.BuscarAsync(
            new AuditoriaFiltroDto(Entidade: "Paciente", EntidadeId: id.ToString(), Tamanho: 200),
            cancellationToken);

    /// <summary>
    /// Adiciona um telefone aos contatos do paciente (append em Patient.telecom),
    /// sem substituir os existentes. Idempotente. Usado pelo agente de voz (CentralIA)
    /// pra registrar o número de quem ligou.
    /// </summary>
    [HttpPost("{id:guid}/telefones")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdicionarTelefone(
        Guid id,
        [FromBody] AdicionarTelefoneRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AdicionarTelefoneAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Backfill de manutenção (ADR-0020 R4): promove a demografia de TODOS os pacientes do blob para
    /// campos FHIR nativos (idempotente/resumível) e re-carimba os telefones já confirmados. Roda em
    /// BACKGROUND (202) — acompanhe pelos logs do server. GATE OPERACIONAL: só com backup do
    /// <c>fhir.patient</c> (o hub não tem undo). Gate de acesso por SincronizacaoPep.Edicao.
    /// </summary>
    [HttpPost("promocao-blob")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult PromoverBlob(
        [FromServices] IServiceScopeFactory escopos,
        [FromServices] ILogger<PacientesController> logger,
        [FromQuery] int throttleMs = 25)
    {
        // Fire-and-forget num escopo próprio (a requisição retorna 202 na hora).
        _ = Task.Run(async () =>
        {
            using var escopo = escopos.CreateScope();
            try
            {
                var svc = escopo.ServiceProvider.GetRequiredService<SMSMais.Core.Pacientes.Promocao.IPromocaoBlobService>();
                await svc.PromoverTodosAsync(throttleMs, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Backfill de promoção blob→nativo falhou.");
            }
        });
        return Accepted(new { mensagem = "Backfill blob→nativo iniciado em background. Acompanhe pelos logs do server." });
    }

    /// <summary>Desativa um paciente (soft delete) — some das listagens.</summary>
    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Reativa um paciente desativado (após confirmação no fluxo de cadastro).</summary>
    [HttpPost("{id:guid}/reativar")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.ReativarAsync(id, cancellationToken);
        return NoContent();
    }
}

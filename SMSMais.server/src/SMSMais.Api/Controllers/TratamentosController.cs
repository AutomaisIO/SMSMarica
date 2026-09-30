using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Tratamentos;
using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Core.UnidadesAtendimento;
using SMSMais.Core.UnidadesAtendimento.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

[ApiController]
[Route("tratamentos")]
public sealed class TratamentosController(
    ITratamentosService service,
    IUnidadesAtendimentoService unidadesAtendimento) : ControllerBase
{
    private readonly ITratamentosService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<TratamentoListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<TratamentoListItemDto>> Listar(
        [FromQuery] Guid? pacienteId,
        [FromQuery] Guid? unidadeAtendimentoId,
        CancellationToken cancellationToken)
    {
        if (pacienteId is { } pid) return await _service.ListarPorPacienteAsync(pid, cancellationToken);
        if (unidadeAtendimentoId is { } uid) return await _service.ListarPorUnidadeAtendimentoAsync(uid, cancellationToken);
        return await _service.ListarAsync(cancellationToken);
    }

    [HttpGet("tipos")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<TipoTratamentoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<TipoTratamentoDto>> ListarTipos(CancellationToken cancellationToken) =>
        await _service.ListarTiposAsync(cancellationToken);

    /// <summary>
    /// Destinos disponíveis (unidades de atendimento ativas) para o seletor do tratamento. Fica sob
    /// a permissão de Tratamentos — como os tipos acima — para quem cadastra tratamento não
    /// precisar do módulo que mantém o cadastro das unidades.
    /// </summary>
    [HttpGet("unidades-atendimento")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<UnidadeAtendimentoOpcaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UnidadeAtendimentoOpcaoDto>> ListarUnidadesAtendimento(CancellationToken cancellationToken) =>
        await unidadesAtendimento.ListarOpcoesAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<TratamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<TratamentoDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    /// <summary>
    /// As datas que a agenda (dias da semana + N sessões ou contínuo) geraria — a prévia do
    /// cadastro e da troca de agenda. O servidor é a única fonte das datas.
    /// </summary>
    [HttpPost("agenda/previa")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<PreviaAgendaDto>(StatusCodes.Status200OK)]
    public ActionResult<PreviaAgendaDto> PreverAgenda([FromBody] AgendaRequest request) =>
        Ok(_service.PreverAgenda(request));

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarTratamentoRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarTratamentoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Troca a agenda a partir da data de início da agenda nova: refaz as sessões pendentes e sem
    /// rota daquele dia em diante. Realizadas, confirmadas e alocadas ficam.
    /// </summary>
    [HttpPut("{id:guid}/agenda")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AlterarAgenda(
        Guid id,
        [FromBody] AgendaRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AlterarAgendaAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Encerrar(Guid id, CancellationToken cancellationToken)
    {
        await _service.EncerrarAsync(id, cancellationToken);
        return NoContent();
    }

    // ---- Sessões

    [HttpPost("{id:guid}/sessoes")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdicionarSessao(
        Guid id,
        [FromBody] AdicionarSessaoRequest request,
        CancellationToken cancellationToken)
    {
        var sessaoId = await _service.AdicionarSessaoAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, sessaoId);
    }

    [HttpPut("{id:guid}/sessoes/{sessaoId:guid}")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AtualizarSessao(
        Guid id,
        Guid sessaoId,
        [FromBody] AtualizarSessaoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarSessaoAsync(id, sessaoId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/sessoes/{sessaoId:guid}")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarSessao(
        Guid id,
        Guid sessaoId,
        CancellationToken cancellationToken)
    {
        await _service.CancelarSessaoAsync(id, sessaoId, cancellationToken);
        return NoContent();
    }

    /// <summary>Quem vai acompanhar o paciente nesta viagem (da lista dele, até o limite do atendimento).</summary>
    [HttpPut("{id:guid}/sessoes/{sessaoId:guid}/acompanhantes")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DefinirAcompanhantesDaSessao(
        Guid id,
        Guid sessaoId,
        [FromBody] DefinirAcompanhantesSessaoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.DefinirAcompanhantesDaSessaoAsync(id, sessaoId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Confirma realização (ou não realização) de uma sessão — registra
    /// quem acompanhou, horários, motorista e veículo de ida/volta.
    /// </summary>
    [HttpPost("{id:guid}/sessoes/{sessaoId:guid}/confirmar")]
    [RequerPermissao(ModuloPermissao.Tratamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmarSessao(
        Guid id,
        Guid sessaoId,
        [FromBody] ConfirmarSessaoRequest request,
        CancellationToken cancellationToken)
    {
        await _service.ConfirmarSessaoAsync(id, sessaoId, request, cancellationToken);
        return NoContent();
    }
}

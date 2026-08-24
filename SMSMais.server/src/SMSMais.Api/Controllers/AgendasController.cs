using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Agendamentos;
using SMSMais.Core.Agendamentos.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Gestão das agendas (grade) dos médicos: cabeçalho, recorrências semanais, janelas
/// avulsas e bloqueios. Os horários livres e a marcação ficam em <see cref="AgendamentosController"/>.
/// Ver ADR-0012.
/// </summary>
[ApiController]
[Route("agendas")]
public sealed class AgendasController(IAgendaService service) : ControllerBase
{
    private readonly IAgendaService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AgendaListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AgendaListItemDto>> Listar(
        [FromQuery] FinalidadeAgenda? finalidade,
        [FromQuery] Guid? unidadeId,
        [FromQuery] Guid? especialidadeId,
        [FromQuery] Guid? medicoId,
        [FromQuery] Guid? equipamentoId,
        [FromQuery] bool incluirInativas,
        CancellationToken cancellationToken) =>
        await _service.ListarAsync(finalidade, unidadeId, especialidadeId, medicoId, equipamentoId, incluirInativas, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<AgendaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AgendaDto> Obter(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarAgendaRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarAgendaRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        await _service.ExcluirAsync(id, cancellationToken);
        return NoContent();
    }

    // ---- Recorrências ----

    [HttpPost("{id:guid}/recorrencias")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType<Guid>(StatusCodes.Status200OK)]
    public async Task<Guid> AdicionarRecorrencia(
        Guid id,
        [FromBody] AdicionarRecorrenciaRequest request,
        CancellationToken cancellationToken) =>
        await _service.AdicionarRecorrenciaAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}/recorrencias/{recorrenciaId:guid}")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoverRecorrencia(Guid id, Guid recorrenciaId, CancellationToken cancellationToken)
    {
        await _service.RemoverRecorrenciaAsync(id, recorrenciaId, cancellationToken);
        return NoContent();
    }

    // ---- Avulsos / Bloqueios ----

    [HttpGet("{id:guid}/disponibilidades")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Consulta)]
    [ProducesResponseType<DisponibilidadesAgendaDto>(StatusCodes.Status200OK)]
    public async Task<DisponibilidadesAgendaDto> ListarDisponibilidades(
        Guid id,
        [FromQuery] DateOnly inicio,
        [FromQuery] DateOnly fim,
        CancellationToken cancellationToken) =>
        await _service.ListarDisponibilidadesAsync(id, inicio, fim, cancellationToken);

    [HttpPost("{id:guid}/avulsos")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType<Guid>(StatusCodes.Status200OK)]
    public async Task<Guid> AdicionarAvulso(
        Guid id,
        [FromBody] AdicionarAvulsoRequest request,
        CancellationToken cancellationToken) =>
        await _service.AdicionarAvulsoAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}/avulsos/{avulsoId:guid}")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoverAvulso(Guid id, Guid avulsoId, CancellationToken cancellationToken)
    {
        await _service.RemoverAvulsoAsync(id, avulsoId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/bloqueios")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType<Guid>(StatusCodes.Status200OK)]
    public async Task<Guid> AdicionarBloqueio(
        Guid id,
        [FromBody] AdicionarBloqueioRequest request,
        CancellationToken cancellationToken) =>
        await _service.AdicionarBloqueioAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}/bloqueios/{bloqueioId:guid}")]
    [RequerPermissao(ModuloPermissao.Agendamentos, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoverBloqueio(Guid id, Guid bloqueioId, CancellationToken cancellationToken)
    {
        await _service.RemoverBloqueioAsync(id, bloqueioId, cancellationToken);
        return NoContent();
    }
}

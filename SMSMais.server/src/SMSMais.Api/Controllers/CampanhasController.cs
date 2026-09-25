using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Notificacoes.Campanhas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Campanhas (ADR-0062) — Mensageria → Campanhas. Período + unidade do SISREG + local e endereço
/// que o paciente vê no lugar dos da unidade, chave da conferência cadastral, envio pelo botão e
/// o alcance (quem recebeu, leu, confirmou, não vai, não respondeu).
/// </summary>
[ApiController]
[Route("campanhas")]
public sealed class CampanhasController(ICampanhaService service) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<CampanhaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<CampanhaDto>> Listar(CancellationToken ct) => await service.ListarAsync(ct);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<CampanhaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<CampanhaDto> Obter(Guid id, CancellationToken ct) => await service.ObterAsync(id, ct);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> Criar([FromBody] SalvarCampanhaRequest request, CancellationToken ct)
    {
        var id = await service.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] SalvarCampanhaRequest request, CancellationToken ct)
    {
        await service.AtualizarAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await service.ExcluirAsync(id, ct);
        return NoContent();
    }

    /// <summary>Agendamentos da campanha com o estado da mensagem e da resposta de cada um.</summary>
    [HttpGet("{id:guid}/alcance")]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<CampanhaAlcanceDto>(StatusCodes.Status200OK)]
    public async Task<CampanhaAlcanceDto> Alcance(Guid id, CancellationToken ct) => await service.AlcanceAsync(id, ct);

    /// <summary>Envia (ou reenvia a quem não respondeu) agora, fora da janela de horário.</summary>
    [HttpPost("{id:guid}/enviar")]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<EnvioCampanhaResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<EnvioCampanhaResultadoDto> Enviar(
        Guid id, [FromBody] EnviarCampanhaRequest request, CancellationToken ct) =>
        await service.EnviarAsync(id, request, ct);
}

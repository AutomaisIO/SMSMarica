using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Notificacoes.Agendamento;
using SMSMarica.Core.Notificacoes.Agendamento.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Gestão das notificações WhatsApp de agendamento (confirmação de exames): acompanhamento de
/// envio/entrega/leitura/falha, resposta do paciente (confirmou/cancelou + motivo) e reenvio.
/// </summary>
[ApiController]
[Route("agendamento-notificacoes")]
public sealed class AgendamentoNotificacoesController(
    IAgendamentoNotificacaoGestaoService service) : ControllerBase
{
    /// <summary>Lista paginada, mais recentes primeiro. Filtros: status do envio, resposta do
    /// paciente, texto (accession/código SISREG/telefone) e período de criação.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaNotificacoesDto>(StatusCodes.Status200OK)]
    public async Task<PaginaNotificacoesDto> Listar(
        [FromQuery] string? status,
        [FromQuery] string? confirmacao,
        [FromQuery] string? texto,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50,
        CancellationToken ct = default) =>
        await service.ListarAsync(new NotificacaoFiltroDto(status, confirmacao, texto, de, ate, pagina, tamanho), ct);

    /// <summary>Detalhe com linha do tempo (tentativas, recibos, erro Meta, magic link).</summary>
    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<NotificacaoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<NotificacaoDetalheDto> Obter(Guid id, CancellationToken ct) =>
        await service.ObterAsync(id, ct);

    /// <summary>Recoloca a notificação na fila (o worker reenvia com magic link novo).</summary>
    [HttpPost("{id:guid}/reenviar")]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reenviar(Guid id, CancellationToken ct)
    {
        await service.ReenviarAsync(id, ct);
        return NoContent();
    }
}

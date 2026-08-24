using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Notificacoes.Comunicacao;
using SMSMarica.Core.Notificacoes.Comunicacao.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Gestão das comunicações WhatsApp ao paciente (confirmação de agendamento, exame liberado,
/// laudo pronto): acompanhamento de envio/entrega/leitura/visualização/falha, resposta do
/// paciente e reenvio. (Rota renomeada de agendamento-notificacoes em 2026-07-05.)
/// </summary>
[ApiController]
[Route("comunicacoes-paciente")]
public sealed class ComunicacoesPacienteController(
    IComunicacaoGestaoService service) : ControllerBase
{
    /// <summary>Lista paginada, mais recentes primeiro. Filtros: status do envio, finalidade,
    /// resposta do paciente, texto (accession/código SISREG/telefone) e período de criação.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaComunicacoesDto>(StatusCodes.Status200OK)]
    public async Task<PaginaComunicacoesDto> Listar(
        [FromQuery] string? status,
        [FromQuery] string? finalidade,
        [FromQuery] string? confirmacao,
        [FromQuery] string? texto,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50,
        CancellationToken ct = default) =>
        await service.ListarAsync(
            new ComunicacaoFiltroDto(status, finalidade, confirmacao, texto, de, ate, pagina, tamanho), ct);

    /// <summary>Detalhe com linha do tempo (tentativas, recibos, erro Meta, magic link).</summary>
    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.NotificacoesAgendamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<ComunicacaoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ComunicacaoDetalheDto> Obter(Guid id, CancellationToken ct) =>
        await service.ObterAsync(id, ct);

    /// <summary>Recoloca a comunicação na fila (o worker reenvia com magic link novo).</summary>
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

using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Consultas;
using SMSMais.Core.Consultas.Dtos;
using SMSMais.Core.SolicitacoesExame;
using SMSMais.Core.SolicitacoesExame.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Solicitações de CONSULTA importadas do SISREG (categoria não-imagem). Sem PACS/laudo — o
/// exame de imagem tem seu próprio módulo. Ver ADR-0021. As comunicações (confirmação por
/// WhatsApp) e os contatos manuais são ancorados na espinha <c>Solicitacao</c>, então o mesmo
/// serviço de histórico dos exames atende consulta passando o id da própria solicitação.
/// </summary>
[ApiController]
[Route("consultas")]
public sealed class ConsultasController(
    IConsultasService service,
    ISolicitacaoHistoricoService historico) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Consultas, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ConsultaListItemDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ConsultaListItemDto>> Listar(
        [FromQuery] Guid? pacienteId,
        [FromQuery] string? busca,
        [FromQuery] DateOnly? dataInicial,
        [FromQuery] DateOnly? dataFinal,
        [FromQuery] StatusSolicitacao? status,
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default)
        => service.ListarAsync(new FiltroConsultasDto(pacienteId, busca, dataInicial, dataFinal, status, limite), cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Consultas, AcoesPermissao.Consulta)]
    [ProducesResponseType<ConsultaDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsultaDetalheDto>> Obter(Guid id, CancellationToken cancellationToken)
        => await service.ObterPorIdAsync(id, cancellationToken) is { } dto ? Ok(dto) : NotFound();

    /// <summary>Histórico do processo de comunicação (WhatsApp de confirmação + contatos manuais).</summary>
    [HttpGet("{id:guid}/historico")]
    [RequerPermissao(ModuloPermissao.Consultas, AcoesPermissao.Consulta)]
    [ProducesResponseType<HistoricoSolicitacaoDto>(StatusCodes.Status200OK)]
    public async Task<HistoricoSolicitacaoDto> Historico(Guid id, CancellationToken cancellationToken)
        => await historico.ObterAsync(id, cancellationToken);

    /// <summary>Reenvia uma comunicação: revoga links/sessões anteriores e reconstrói com os
    /// dados atuais do paciente (telefone certo, link novo).</summary>
    [HttpPost("{id:guid}/comunicacoes/{comunicacaoId:guid}/reenviar")]
    [RequerPermissao(ModuloPermissao.Consultas, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReenviarComunicacao(
        Guid id, Guid comunicacaoId,
        [FromServices] SMSMais.Core.Notificacoes.Comunicacao.IComunicacaoPacienteService comunicacoes,
        CancellationToken cancellationToken)
    {
        await comunicacoes.ReenviarAsync(id, comunicacaoId, cancellationToken);
        return NoContent();
    }

    /// <summary>Registra um contato MANUAL com o paciente ("liguei, não atendeu"...). Append-only.</summary>
    [HttpPost("{id:guid}/contatos")]
    [RequerPermissao(ModuloPermissao.Consultas, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarContato(
        Guid id, [FromBody] RegistrarContatoRequest request, CancellationToken cancellationToken)
    {
        await historico.RegistrarContatoAsync(id, request, cancellationToken);
        return NoContent();
    }
}

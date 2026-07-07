using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Associacoes;
using SMSMarica.Core.Associacoes.Dtos;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Associação manual de um estudo do PACS a uma solicitação de exame (para exames
/// que chegaram sem worklist). Governada pelo módulo Pacs.
/// </summary>
[ApiController]
[Route("exames/associacoes")]
public sealed class ExameAssociacaoController(
    IExameAssociacaoService service,
    ISolicitacoesExameService solicitacoes) : ControllerBase
{
    /// <summary>Associa um estudo a uma solicitação (pelo número SMS do pedido).</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Edicao)]
    [ProducesResponseType<ExameAssociacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ExameAssociacaoDto> Associar(
        [FromBody] AssociarExameRequest request, CancellationToken cancellationToken) =>
        await service.AssociarAsync(request, cancellationToken: cancellationToken);

    /// <summary>
    /// Resincronização sob demanda (rede de segurança, PACS-driven): varre os studies
    /// recentes do PACS (pela data do EXAME) e concilia cada um com a solicitação pelo
    /// AccessionNumber / PatientID = nº SMS / StudyUID de worklist. Idempotente.
    /// </summary>
    [HttpPost("resincronizar")]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Edicao)]
    [ProducesResponseType<ResincronizacaoResultadoDto>(StatusCodes.Status200OK)]
    public async Task<ResincronizacaoResultadoDto> Resincronizar(CancellationToken cancellationToken) =>
        await service.ResincronizarAsync(cancellationToken);

    /// <summary>Desassocia um estudo. Bloqueado se houver laudo finalizado.</summary>
    [HttpDelete("{studyInstanceUID}")]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desassociar(string studyInstanceUID, CancellationToken cancellationToken)
    {
        await service.DesassociarAsync(studyInstanceUID, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Lote para a listagem PACS: vínculos (explícito ou implícito) por StudyInstanceUID.
    /// Query: <c>?studyUIDs=1.2,1.3,...</c> (CSV) ou repetido.
    /// </summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ExameAssociacaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ExameAssociacaoDto>> Lote(
        [FromQuery(Name = "studyUIDs")] string[] studyUIDs, CancellationToken cancellationToken)
    {
        var uids = studyUIDs is null
            ? Array.Empty<string>()
            : [.. studyUIDs.SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))];
        return await service.ObterPorStudyUidsAsync(uids, cancellationToken);
    }

    /// <summary>
    /// Pré-visualização do modal: solicitação enriquecida (paciente + resumo) pelo
    /// número SMS. Sob permissão Pacs (o operador da tela não precisa de SolicitacoesExame).
    /// </summary>
    [HttpGet("preview/{accession}")]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Consulta)]
    [ProducesResponseType<SolicitacaoExameDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Preview(string accession, CancellationToken cancellationToken)
    {
        var dto = await solicitacoes.ObterPorAccessionAsync(accession, cancellationToken);
        return dto is null ? NoContent() : Ok(dto);
    }
}

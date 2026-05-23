using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.EstudoAnotacoes;
using SMSMarica.Core.EstudoAnotacoes.Dtos;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Anotações persistidas dos estudos PACS. Cada save cria uma nova versão
/// imutável carimbada com o usuário (médico) autenticado.
/// </summary>
[ApiController]
[Route("estudos/{studyInstanceUID}/anotacoes")]
public sealed class EstudoAnotacoesController(IEstudoAnotacoesService service) : ControllerBase
{
    private readonly IEstudoAnotacoesService _service = service;

    /// <summary>
    /// Retorna a versão mais recente das anotações do estudo. 204 quando
    /// ainda não há nada salvo (para o front saber que precisa começar do zero).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<EstudoAnotacaoVersaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ObterVersaoAtual(string studyInstanceUID, CancellationToken cancellationToken)
    {
        var dto = await _service.ObterVersaoAtualAsync(studyInstanceUID, cancellationToken);
        return dto is null ? NoContent() : Ok(dto);
    }

    /// <summary>Lista o histórico completo do estudo (mais recente primeiro), sem payload.</summary>
    [HttpGet("historico")]
    [ProducesResponseType<IReadOnlyList<EstudoAnotacaoVersaoResumoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<EstudoAnotacaoVersaoResumoDto>> ListarHistorico(
        string studyInstanceUID,
        CancellationToken cancellationToken) =>
        await _service.ListarHistoricoAsync(studyInstanceUID, cancellationToken);

    /// <summary>Retorna uma versão específica com o payload completo (para restaurar/inspecionar).</summary>
    [HttpGet("versoes/{versao:int}")]
    [ProducesResponseType<EstudoAnotacaoVersaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<EstudoAnotacaoVersaoDto> ObterVersao(
        string studyInstanceUID,
        int versao,
        CancellationToken cancellationToken) =>
        await _service.ObterVersaoAsync(studyInstanceUID, versao, cancellationToken);

    /// <summary>Cria uma nova versão (append-only) carimbada com o usuário autenticado.</summary>
    [HttpPost]
    [ProducesResponseType<EstudoAnotacaoVersaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Salvar(
        string studyInstanceUID,
        [FromBody] SalvarEstudoAnotacaoRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        var dto = await _service.SalvarAsync(studyInstanceUID, usuarioId, request, cancellationToken);
        return CreatedAtAction(nameof(ObterVersao), new { studyInstanceUID, versao = dto.Versao }, dto);
    }

    private Guid ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id)) return id;
        throw new ValidacaoException("auth.sub_invalido", "Token sem identificação do usuário.");
    }
}

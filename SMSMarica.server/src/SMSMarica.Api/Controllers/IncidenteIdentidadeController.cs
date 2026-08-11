using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Associacoes;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

public sealed record DescartarIncidenteRequest(string Nota);

/// <summary>
/// Alarme de "este exame pode estar no paciente errado" e a quarentena que ele impõe.
///
/// <para>Abrir exige apenas <see cref="ModuloPermissao.Pacs"/> Consulta — de propósito: quem
/// percebe a troca costuma ser quem está olhando o exame (a médica ao laudar, a recepção ao
/// conferir), não o administrador. Congelar é barato e reversível; deixar um exame trocado
/// circular, não. Resolver ou descartar exige o módulo de correção.</para>
/// </summary>
[ApiController]
[Route("exames/incidentes-identidade")]
public sealed class IncidenteIdentidadeController(
    IQuarentenaIdentidadeService service,
    IDetectorTrocaIdentidadeService detector) : ControllerBase
{
    /// <summary>
    /// Varredura do "par órfão da recepção": exame realizado por quem não passou pela recepção no
    /// dia, tendo no mesmo dia e unidade alguém autorizado e sem imagens.
    /// <para><paramref name="quarentenar"/> falso (padrão) só RELATA — é assim que se mede o
    /// falso-positivo antes de deixar o detector congelar exame sozinho.</para>
    /// </summary>
    [HttpPost("varredura")]
    [RequerPermissao(ModuloPermissao.CorrecaoIdentidadeExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<SuspeitaTrocaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Varredura(
        [FromQuery] DateOnly? dia, [FromQuery] bool quarentenar, CancellationToken cancellationToken)
    {
        var alvo = dia ?? DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        if (!quarentenar) return Ok(await detector.VarrerAsync(alvo, cancellationToken));

        var abertos = await detector.VarrerEQuarentenarAsync(alvo, cancellationToken);
        return Ok(new { dia = alvo, incidentesAbertos = abertos });
    }

    /// <summary>Congela o exame: bloqueia laudo, aviso ao paciente e conciliação automática.</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Consulta)]
    [ProducesResponseType<IncidenteIdentidadeDto>(StatusCodes.Status200OK)]
    public async Task<IncidenteIdentidadeDto> Abrir(
        [FromBody] AbrirIncidenteRequest request, CancellationToken cancellationToken) =>
        await service.AbrirAsync(request, automatico: false, cancellationToken);

    /// <summary>Fila de exames em conferência (sem filtro: também os já encerrados).</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<IncidenteIdentidadeDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<IncidenteIdentidadeDto>> Listar(
        [FromQuery] StatusIncidenteIdentidade? status, CancellationToken cancellationToken) =>
        await service.ListarAsync(status, cancellationToken);

    /// <summary>Falso alarme: levanta a quarentena sem corrigir nada.</summary>
    [HttpPost("{id:guid}/descartar")]
    [RequerPermissao(ModuloPermissao.CorrecaoIdentidadeExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Descartar(
        Guid id, [FromBody] DescartarIncidenteRequest request, CancellationToken cancellationToken)
    {
        await service.DescartarAsync(id, request.Nota, cancellationToken);
        return NoContent();
    }
}

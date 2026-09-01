using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.PendenciasCadastro;
using SMSMais.Core.PendenciasCadastro.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Pendências de ajuste de cadastro ("números errados"): a fila de casos em que o cidadão avisou
/// que o número não é dele. O robô só registra — a recepção resolve o cadastro aqui.
/// </summary>
[ApiController]
[Route("pendencias-cadastro")]
public sealed class PendenciasCadastroController(IPendenciaCadastroService service) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.AjusteCadastro, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<PendenciaCadastroListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PendenciaCadastroListItemDto>> Listar(
        [FromQuery] StatusPendenciaCadastro? status, CancellationToken ct) =>
        await service.ListarAsync(status, ct);

    /// <summary>Varredura manual: procura "não sou essa pessoa" nas conversas recentes (padrões
    /// amplos + confirmação pelo modelo) e registra as pendências que faltam.</summary>
    [HttpPost("varredura")]
    [RequerPermissao(ModuloPermissao.AjusteCadastro, AcoesPermissao.Edicao)]
    [ProducesResponseType<VarreduraContatoNegadoResultadoDto>(StatusCodes.Status200OK)]
    public async Task<VarreduraContatoNegadoResultadoDto> Varredura(
        [FromBody] VarreduraContatoNegadoRequest request,
        [FromServices] IVarreduraContatoNegadoService varredura,
        CancellationToken ct) =>
        await varredura.ExecutarAsync(request.Horas, ct);

    [HttpPost("{id:guid}/resolver")]
    [RequerPermissao(ModuloPermissao.AjusteCadastro, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolver(Guid id, [FromBody] ResolverPendenciaRequest request, CancellationToken ct)
    {
        await service.ResolverAsync(id, request.Nota, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/ignorar")]
    [RequerPermissao(ModuloPermissao.AjusteCadastro, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ignorar(Guid id, [FromBody] ResolverPendenciaRequest request, CancellationToken ct)
    {
        await service.IgnorarAsync(id, request.Nota, ct);
        return NoContent();
    }
}

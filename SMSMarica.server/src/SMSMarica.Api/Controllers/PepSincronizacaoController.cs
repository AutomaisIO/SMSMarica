using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Integracoes.Pep;
using SMSMarica.Core.Integracoes.Pep.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Sincronização/importação de bases de PEP (Salux e futuros) para o hub FHIR. Seleciona a
/// base (IaFonte), dispara um job em background e acompanha status/progresso. Ver ADR-0014.
/// </summary>
[ApiController]
[Route("pep-sincronizacao")]
public sealed class PepSincronizacaoController(IPepSincronizacaoService service) : ControllerBase
{
    /// <summary>Bases disponíveis para importação (com flag de suporte e última sincronização).</summary>
    [HttpGet("bases")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<BasePepDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<BasePepDto>> Bases(CancellationToken ct) =>
        await service.ListarBasesAsync(ct);

    /// <summary>Dispara uma importação (background). Devolve o id da execução criada.</summary>
    [HttpPost("importar")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Importar([FromBody] IniciarImportacaoRequest request, CancellationToken ct)
    {
        var execucaoId = await service.IniciarAsync(request, ct);
        return AcceptedAtAction(nameof(Status), new { }, new { execucaoId });
    }

    /// <summary>Para a importação em andamento (cancelamento cooperativo).</summary>
    [HttpPost("cancelar")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(CancellationToken ct)
    {
        await service.CancelarAsync(ct);
        return Accepted();
    }

    /// <summary>Status do run vivo (se houver) ou da última execução.</summary>
    [HttpGet("status")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<StatusImportacaoDto>(StatusCodes.Status200OK)]
    public async Task<StatusImportacaoDto> Status(CancellationToken ct) =>
        await service.ObterStatusAsync(ct);

    /// <summary>Histórico de execuções (mais recentes primeiro), opcionalmente por base.</summary>
    [HttpGet("execucoes")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ExecucaoImportacaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ExecucaoImportacaoDto>> Execucoes([FromQuery] Guid? fonteId, CancellationToken ct) =>
        await service.ListarExecucoesAsync(fonteId, ct);

    /// <summary>
    /// Falhas duráveis de importação (mais recentes primeiro). Filtra por execução/base e, com
    /// <paramref name="somentePendentes"/>, só as ainda não resolvidas — insumo do reimport por cd.
    /// </summary>
    [HttpGet("falhas")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<FalhaImportacaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<FalhaImportacaoDto>> Falhas(
        [FromQuery] Guid? execucaoId, [FromQuery] Guid? fonteId, [FromQuery] bool somentePendentes,
        CancellationToken ct) =>
        await service.ListarFalhasAsync(execucaoId, fonteId, somentePendentes, ct);
}

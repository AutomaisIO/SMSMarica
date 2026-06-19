using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Faturamento;
using SMSMarica.Core.Faturamento.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>Faturamento SUS/BPA do transporte (FT10): contabilização, relatórios e configuração.</summary>
[ApiController]
[Route("faturamento")]
public sealed class FaturamentoController(IFaturamentoService service) : ControllerBase
{
    private readonly IFaturamentoService _service = service;

    /// <summary>Contabiliza (cria/atualiza) o faturamento de uma sessão realizada.</summary>
    [HttpPost("contabilizar/{sessaoId:guid}")]
    [RequerPermissao(ModuloPermissao.Faturamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegistroFaturamentoDto>(StatusCodes.Status200OK)]
    public async Task<RegistroFaturamentoDto> Contabilizar(Guid sessaoId, CancellationToken cancellationToken) =>
        await _service.ContabilizarAsync(sessaoId, cancellationToken);

    /// <summary>Lista registros de faturamento por competência (AAAAMM) ou período.</summary>
    [HttpGet("registros")]
    [RequerPermissao(ModuloPermissao.Faturamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<RegistroFaturamentoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<RegistroFaturamentoDto>> Registros(
        [FromQuery] int? competencia,
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        CancellationToken cancellationToken) =>
        await _service.ListarAsync(competencia, de, ate, cancellationToken);

    /// <summary>Resumo agrupado por paciente/motorista/veículo/tipo de tratamento/unidade no período.</summary>
    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.Faturamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<ResumoFaturamentoDto>(StatusCodes.Status200OK)]
    public async Task<ResumoFaturamentoDto> Resumo(
        [FromQuery] DimensaoFaturamento dimensao = DimensaoFaturamento.Paciente,
        [FromQuery] int? competencia = null,
        [FromQuery] DateOnly? de = null,
        [FromQuery] DateOnly? ate = null,
        CancellationToken cancellationToken = default) =>
        await _service.ResumoAsync(dimensao, competencia, de, ate, cancellationToken);

    [HttpGet("config")]
    [RequerPermissao(ModuloPermissao.Faturamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<TfdConfigFaturamentoDto>(StatusCodes.Status200OK)]
    public async Task<TfdConfigFaturamentoDto> ObterConfig(CancellationToken cancellationToken) =>
        await _service.ObterConfigAsync(cancellationToken);

    [HttpPut("config")]
    [RequerPermissao(ModuloPermissao.Faturamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarConfig(
        [FromBody] AtualizarTfdConfigFaturamentoRequest request, CancellationToken cancellationToken)
    {
        await _service.AtualizarConfigAsync(request, cancellationToken);
        return NoContent();
    }
}

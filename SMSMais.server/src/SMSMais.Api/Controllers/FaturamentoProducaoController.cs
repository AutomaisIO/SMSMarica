using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.FaturamentoProducao;
using SMSMais.Core.FaturamentoProducao.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Faturamento de PRODUÇÃO (BPA de procedimentos/consultas a partir da nossa base — SISREG, com
/// comparecimento). NÃO confundir com <see cref="FaturamentoController"/> (BPA do transporte/TFD).
/// Consulta = gerar/baixar o BPA das unidades habilitadas; Edição = Configurações (habilitar unidades).
/// </summary>
[ApiController]
[Route("faturamento-producao")]
public sealed class FaturamentoProducaoController(IBpaProducaoService service) : ControllerBase
{
    /// <summary>Unidades liberadas para gerar o BPA (habilitadas ∩ escopo) — alimenta o seletor.</summary>
    [HttpGet("unidades-habilitadas")]
    [RequerPermissao(ModuloPermissao.FaturamentoProducao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<UnidadeHabilitadaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UnidadeHabilitadaDto>> UnidadesHabilitadas(CancellationToken ct) =>
        await service.ListarUnidadesHabilitadasAsync(ct);

    /// <summary>Preview da competência: linhas (válidas + pendências) + estatísticas de qualidade.</summary>
    [HttpGet("preview")]
    [RequerPermissao(ModuloPermissao.FaturamentoProducao, AcoesPermissao.Consulta)]
    [ProducesResponseType<BpaPreviewDto>(StatusCodes.Status200OK)]
    public async Task<BpaPreviewDto> Preview(
        [FromQuery] Guid unidadeId, [FromQuery] int competencia, CancellationToken ct) =>
        await service.PreviewAsync(unidadeId, competencia, ct);

    /// <summary>Gera e baixa o arquivo BPA Magnético (só as linhas válidas) da competência.</summary>
    [HttpGet("arquivo")]
    [RequerPermissao(ModuloPermissao.FaturamentoProducao, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Arquivo(
        [FromQuery] Guid unidadeId, [FromQuery] int competencia, CancellationToken ct)
    {
        var (bytes, nome) = await service.GerarArquivoAsync(unidadeId, competencia, ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(bytes, "text/plain; charset=iso-8859-1", nome);
    }

    /// <summary>Configurações: todas as unidades do escopo + flag de habilitada.</summary>
    [HttpGet("configuracao/unidades")]
    [RequerPermissao(ModuloPermissao.FaturamentoProducao, AcoesPermissao.Edicao)]
    [ProducesResponseType<IReadOnlyList<ConfigUnidadeDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ConfigUnidadeDto>> Configuracao(CancellationToken ct) =>
        await service.ListarConfiguracaoAsync(ct);

    /// <summary>Habilita/desabilita uma unidade para o faturamento de produção.</summary>
    [HttpPut("configuracao/unidades/{unidadeId:guid}")]
    [RequerPermissao(ModuloPermissao.FaturamentoProducao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DefinirHabilitada(
        Guid unidadeId, [FromBody] DefinirUnidadeHabilitadaRequest request, CancellationToken ct)
    {
        await service.DefinirHabilitadaAsync(unidadeId, request.Habilitada, ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Regras;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Cadastro das regras de elegibilidade por procedimento (plano 03).
///
/// <para>É configuração da regulação (módulo 51): quem cadastra regra decide o que a rede inteira
/// pode ou não pedir. A unidade solicitante só as vê aplicadas ao próprio pedido.</para>
/// </summary>
[ApiController]
[Route("regulacao/regras")]
public sealed class RegulacaoRegrasController(IRegulacaoRegraService servico) : ControllerBase
{
    /// <summary>Limite de corpo da importação: o CSV dos manuais tem ~1.200 linhas.</summary>
    private const int LimiteCorpoBytes = 20 * 1024 * 1024;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<RegulacaoRegraDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<RegulacaoRegraDto>> Listar(
        [FromQuery] Guid procedimentoId,
        [FromQuery] SistemaRegulacao? sistema,
        [FromQuery] bool inativas,
        CancellationToken cancellationToken) =>
        servico.ListarAsync(procedimentoId, sistema, inativas, cancellationToken);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Inclusao)]
    [ProducesResponseType<RegulacaoRegraDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoRegraDto> Criar(
        [FromBody] SalvarRegulacaoRegraRequest req, CancellationToken cancellationToken) =>
        servico.CriarAsync(req, cancellationToken);

    /// <summary>
    /// Corrigir uma regra cria a versão seguinte — a anterior é desativada, não sobrescrita. É o
    /// que mantém legível, meses depois, por que um pedido foi barrado.
    /// </summary>
    [HttpPost("{id:guid}/nova-versao")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<RegulacaoRegraDto>(StatusCodes.Status200OK)]
    public Task<RegulacaoRegraDto> NovaVersao(
        Guid id, [FromBody] SalvarRegulacaoRegraRequest req, CancellationToken cancellationToken) =>
        servico.NovaVersaoAsync(id, req, cancellationToken);

    [HttpPatch("{id:guid}/ativo")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Ativar(
        Guid id, [FromBody] AtivarRegraRequest req, CancellationToken cancellationToken)
    {
        await servico.AtivarAsync(id, req.Ativo, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        await servico.ExcluirAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Importa o CSV extraído dos manuais. <b>Cria tudo inativo</b> — o resultado diz quantas
    /// entraram e o que não casou com o catálogo.
    /// </summary>
    [HttpPost("importar-csv")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [RequestSizeLimit(LimiteCorpoBytes)]
    [ProducesResponseType<ImportacaoRegrasResultadoDto>(StatusCodes.Status200OK)]
    public async Task<ImportacaoRegrasResultadoDto> ImportarCsv(
        IFormFile arquivo, CancellationToken cancellationToken)
    {
        using var stream = arquivo.OpenReadStream();
        return await servico.ImportarCsvAsync(stream, cancellationToken);
    }

    public sealed record AtivarRegraRequest(bool Ativo);
}

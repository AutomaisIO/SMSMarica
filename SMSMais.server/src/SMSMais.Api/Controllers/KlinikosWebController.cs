using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.KlinikosWeb;
using SMSMais.Core.Integracoes.KlinikosWeb.Escrita;
using SMSMais.Core.Integracoes.KlinikosWeb.Varredura;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Gatilhos do conector web do Klinikos (ADITIVO — não substitui a estratégia SQL das UPAs).
///
/// <para><b>Dry-run</b> e <b>paridade</b> são SOMENTE LEITURA (não escrevem no hub). <b>Gravar</b>
/// e <b>backfill</b> só funcionam com a trava <c>KlinikosWeb:EscritaHabilitada=true</c> e apenas
/// no Conde (as UPAs têm dono SQL). Nada aqui roda sozinho — é sempre por chamada explícita.</para>
/// </summary>
[ApiController]
[Route("klinikos-web")]
public sealed class KlinikosWebController(
    IKlinikosWebSincronizacaoService sincronizacao,
    IKlinikosWebParidadeService paridade,
    IKlinikosWebEscritaService escrita) : ControllerBase
{
    /// <summary>Contagens da espinha de um dia (407+667), sem gravar. Para conferir os números.</summary>
    [HttpGet("dry-run")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<ResumoDryRun>(StatusCodes.Status200OK)]
    public async Task<ResumoDryRun> DryRun(
        [FromQuery] string slug, [FromQuery] DateOnly dia, CancellationToken ct) =>
        await sincronizacao.DryRunEspinhaAsync(slug, dia, ct);

    /// <summary>
    /// Paridade SQL × web (só leitura): compara os recursos web em memória com o que o hub já tem
    /// via SQL, para os mesmos boletins. Critério de aceite antes de substituir. Rodar numa UPA.
    /// </summary>
    [HttpGet("paridade")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<ParidadeRelatorio>(StatusCodes.Status200OK)]
    public async Task<ParidadeRelatorio> Paridade(
        [FromQuery] string slug, [FromQuery] DateOnly dia, [FromQuery] int amostra = 20,
        CancellationToken ct = default) =>
        await paridade.CompararAsync(slug, dia, amostra, ct);

    /// <summary>Grava a espinha de um dia no hub. Só Conde e só com a escrita habilitada.</summary>
    [HttpPost("gravar")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType<ResumoEscrita>(StatusCodes.Status200OK)]
    public async Task<ResumoEscrita> Gravar(
        [FromQuery] string slug, [FromQuery] DateOnly dia, CancellationToken ct) =>
        await escrita.GravarEspinhaAsync(slug, dia, ct);

    /// <summary>Backfill do buraco da migração (Conde: 08/08→hoje), dia a dia. Só com escrita habilitada.</summary>
    [HttpPost("backfill")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType<IReadOnlyList<ResumoEscrita>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ResumoEscrita>> Backfill(
        [FromQuery] string slug, [FromQuery] DateOnly de, [FromQuery] DateOnly ate,
        CancellationToken ct) =>
        await escrita.BackfillAsync(slug, de, ate, ct);
}

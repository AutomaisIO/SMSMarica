using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Integracoes.SisregWeb.Mapeamento;
using SMSMarica.Core.Integracoes.SisregWeb.Mapeamento.Dtos;
using SMSMarica.Core.Integracoes.SisregWeb.Varredura.Sigtap;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Mapeamento do SISREG por unidade: profissionais executantes, seus procedimentos e o
/// habilita/desabilita que define o que entra na varredura de agenda.
///
/// <para><b>Os endpoints de mapeamento exigem UMA unidade selecionada</b> (header
/// <c>X-Unidade-Id</c>). Na visão "todas as unidades" o service recusa com
/// <c>sisreg.unidade_obrigatoria</c> (400): profissionais e procedimentos são sempre de uma
/// unidade, e mapear "todas" de uma vez não quer dizer nada. A credencial do SISREG é uma só,
/// global, e vive na tela de Integrações.</para>
/// </summary>
[ApiController]
[Route("sisreg/mapeamento")]
public sealed class SisregMapeamentoController(
    ISisregMapeamentoService mapeamentoService,
    IMapeadorSigtapSisreg mapeadorSigtap) : ControllerBase
{
    private readonly ISisregMapeamentoService _mapeamentoService = mapeamentoService;
    private readonly IMapeadorSigtapSisreg _mapeadorSigtap = mapeadorSigtap;

    // ---------------------------------------------------------------- mapeamento

    /// <summary>Mapeamento persistido da unidade selecionada (não vai ao SISREG).</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<SisregMapeamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<SisregMapeamentoDto> Obter(CancellationToken cancellationToken) =>
        await _mapeamentoService.ObterAsync(cancellationToken);

    /// <summary>
    /// Vai ao SISREG e reconcilia o mapeamento, preservando as habilitações já escolhidas.
    /// Custa uma requisição por profissional — por isso não é automático.
    /// </summary>
    [HttpPost("atualizar")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<SisregMapeamentoAtualizacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<SisregMapeamentoAtualizacaoDto> Atualizar(CancellationToken cancellationToken) =>
        await _mapeamentoService.AtualizarAsync(cancellationToken);

    [HttpPut("profissionais/{id:guid}/habilitacao")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AlternarProfissional(
        Guid id, [FromBody] AlternarHabilitacaoRequest request, CancellationToken cancellationToken)
    {
        await _mapeamentoService.AlternarProfissionalAsync(id, request.Habilitado, cancellationToken);
        return NoContent();
    }

    [HttpPut("procedimentos/{id:guid}/habilitacao")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AlternarProcedimento(
        Guid id, [FromBody] AlternarHabilitacaoRequest request, CancellationToken cancellationToken)
    {
        await _mapeamentoService.AlternarProcedimentoAsync(id, request.Habilitado, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Liga/desliga o aviso por WhatsApp ao paciente quando este procedimento é importado NESTA
    /// unidade. Aplica a todas as linhas do mesmo procedimento na unidade — ele costuma aparecer
    /// sob vários profissionais, e a decisão é do procedimento, não do par com o profissional.
    /// </summary>
    [HttpPut("procedimentos/{id:guid}/confirmacao")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AlternarEnvioConfirmacao(
        Guid id, [FromBody] AlternarEnvioConfirmacaoRequest request, CancellationToken cancellationToken)
    {
        var afetados = await _mapeamentoService.AlternarEnvioConfirmacaoAsync(
            id, request.Enviar, cancellationToken);
        return Ok(new { afetados });
    }

    [HttpPut("profissionais/habilitacao-lote")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AlternarProfissionaisEmLote(
        [FromBody] AlternarEmLoteRequest request, CancellationToken cancellationToken)
    {
        await _mapeamentoService.AlternarProfissionaisEmLoteAsync(request.Ids, request.Habilitado, cancellationToken);
        return NoContent();
    }

    /// <summary>Sincroniza os profissionais habilitados com o hub FHIR (Practitioner), dedup por CPF.</summary>
    [HttpPost("sincronizar-fhir")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<SisregSincronizacaoFhirDto>(StatusCodes.Status200OK)]
    public async Task<SisregSincronizacaoFhirDto> SincronizarFhir(CancellationToken cancellationToken) =>
        await _mapeamentoService.SincronizarFhirAsync(cancellationToken);

    // ------------------------------------------------------- de-para pa → SIGTAP

    /// <summary>
    /// Catálogo do de-para entre o procedimento do SISREG (o <c>pa</c>) e o SIGTAP oficial.
    /// É global (não por unidade): o <c>pa</c> é identificador nacional.
    /// </summary>
    [HttpGet("procedimentos-sigtap")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ProcedimentoSigtapDeParaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ProcedimentoSigtapDeParaDto>> ListarDeParaSigtap(
        [FromQuery] bool somenteNaoConfirmados, CancellationToken cancellationToken) =>
        await _mapeadorSigtap.ListarAsync(somenteNaoConfirmados, cancellationToken);

    /// <summary>
    /// Roda a heurística de nome sobre os procedimentos ainda não confirmados. Só nome idêntico
    /// (depois de normalizar) é confirmado sozinho — o resto vira sugestão para o operador.
    /// </summary>
    [HttpPost("procedimentos-sigtap/sugerir")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SugerirDeParaSigtap(CancellationToken cancellationToken)
    {
        var sugeridos = await _mapeadorSigtap.SugerirAsync(cancellationToken);
        return Ok(new { sugeridos });
    }

    /// <summary>Confirma o de-para. É o que libera o procedimento para entrar na varredura.</summary>
    [HttpPut("procedimentos-sigtap/{id:guid}")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<ProcedimentoSigtapDeParaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ProcedimentoSigtapDeParaDto> ConfirmarDeParaSigtap(
        Guid id, [FromBody] ConfirmarDeParaSigtapRequest request, CancellationToken cancellationToken) =>
        await _mapeadorSigtap.ConfirmarAsync(id, request.ProcedimentoSigtapId, cancellationToken);
}

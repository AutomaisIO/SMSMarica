using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Integracoes.SisregWeb.Credencial;
using SMSMarica.Core.Integracoes.SisregWeb.Credencial.Dtos;
using SMSMarica.Core.Integracoes.SisregWeb.Mapeamento;
using SMSMarica.Core.Integracoes.SisregWeb.Mapeamento.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Mapeamento do SISREG por unidade: profissionais executantes, seus procedimentos e o
/// habilita/desabilita que define o que entra na varredura de agenda — mais a credencial de
/// operador do SISREG daquela unidade.
///
/// <para><b>Todos os endpoints exigem UMA unidade selecionada</b> (header <c>X-Unidade-Id</c>).
/// Na visão "todas as unidades" o service recusa com <c>sisreg.unidade_obrigatoria</c> (400):
/// a credencial do SISREG é de um operador que enxerga uma unidade só, então não há como
/// decidir contra qual autenticar.</para>
/// </summary>
[ApiController]
[Route("sisreg/mapeamento")]
public sealed class SisregMapeamentoController(
    ISisregMapeamentoService mapeamentoService,
    ISisregCredencialUnidadeService credencialService) : ControllerBase
{
    private readonly ISisregMapeamentoService _mapeamentoService = mapeamentoService;
    private readonly ISisregCredencialUnidadeService _credencialService = credencialService;

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

    // ---------------------------------------------------------------- credencial

    /// <summary>Credencial SISREG da unidade. A senha nunca é devolvida — só o usuário.</summary>
    [HttpGet("credencial")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<SisregCredencialUnidadeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<SisregCredencialUnidadeDto> ObterCredencial(CancellationToken cancellationToken) =>
        await _credencialService.ObterAsync(cancellationToken);

    /// <summary>
    /// Troca usuário/senha do SISREG da unidade. Só grava se o SISREG autenticar E a unidade
    /// da sessão conferir com a unidade selecionada.
    /// </summary>
    [HttpPut("credencial")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<SisregAutenticacaoResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<SisregAutenticacaoResultadoDto> SalvarCredencial(
        [FromBody] SalvarSisregCredencialUnidadeRequest request, CancellationToken cancellationToken) =>
        await _credencialService.SalvarAsync(request, cancellationToken);

    /// <summary>Reautentica a credencial gravada e revalida o vínculo com a unidade.</summary>
    [HttpPost("credencial/testar")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<SisregAutenticacaoResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<SisregAutenticacaoResultadoDto> TestarCredencial(CancellationToken cancellationToken) =>
        await _credencialService.TestarAsync(cancellationToken);

    /// <summary>Remove a credencial da unidade (volta a usar a credencial global).</summary>
    [HttpDelete("credencial")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoverCredencial(CancellationToken cancellationToken)
    {
        await _credencialService.RemoverAsync(cancellationToken);
        return NoContent();
    }
}

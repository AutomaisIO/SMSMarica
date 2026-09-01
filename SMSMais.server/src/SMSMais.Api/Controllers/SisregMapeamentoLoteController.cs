using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote;
using SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// "SISREG Sincroniza tudo" (#118): reconcilia o mapeamento (profissionais/procedimentos + vínculo
/// FHIR) de <b>todas</b> as unidades configuradas de uma vez, com botão manual e agendamento diário.
///
/// <para><b>Não é por unidade</b> (ao contrário de <c>sisreg/mapeamento</c> e
/// <c>sisreg/varredura</c>): estes endpoints operam a rede inteira, então não usam
/// <c>X-Unidade-Id</c>. Um lote por vez em toda a instalação, sequencial, respeitando o teto de
/// requisições/hora do SISREG.</para>
/// </summary>
[ApiController]
[Route("sisreg/mapeamento/lote")]
public sealed class SisregMapeamentoLoteController(ISisregMapeamentoLoteService lote) : ControllerBase
{
    private readonly ISisregMapeamentoLoteService _lote = lote;

    /// <summary>
    /// Dispara AGORA a sincronização de todas as unidades. 202: roda no servidor, fechar a aba não
    /// interrompe. Acompanhe por <c>GET status</c>.
    /// </summary>
    [HttpPost("sincronizar")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<MapeamentoLoteAceitoDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Sincronizar(CancellationToken cancellationToken)
    {
        var aceito = await _lote.IniciarAsync(cancellationToken);
        return Accepted(aceito);
    }

    /// <summary>Progresso do lote em curso, ou 204 se não houver nenhum.</summary>
    [HttpGet("status")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<MapeamentoLoteStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Status()
    {
        var status = _lote.ObterStatus();
        return status is null ? NoContent() : Ok(status);
    }

    /// <summary>Para o lote em curso. O que já entrou permanece — cada unidade é gravada ao concluir.</summary>
    [HttpPost("cancelar")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Cancelar() => Ok(new { cancelada = _lote.Cancelar() });

    /// <summary>Sincronizações recentes — o que entrou em cada uma, depois do fato.</summary>
    [HttpGet("execucoes")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<MapeamentoLoteExecucaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<MapeamentoLoteExecucaoDto>> Execucoes(
        [FromQuery] int limite, CancellationToken cancellationToken) =>
        await _lote.ListarExecucoesAsync(limite <= 0 ? 10 : limite, cancellationToken);

    /// <summary>Detalhe por unidade de uma sincronização: quantos médicos e procedimentos vieram
    /// de cada uma, e o motivo de quem ficou de fora.</summary>
    [HttpGet("execucoes/{id:guid}/itens")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<MapeamentoLoteExecucaoItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<MapeamentoLoteExecucaoItemDto>> Itens(
        Guid id, CancellationToken cancellationToken) =>
        await _lote.ListarItensAsync(id, cancellationToken);

    /// <summary>
    /// Programa a rede inteira para o sincronismo diário: habilita todos os médicos e procedimentos
    /// já mapeados e liga a varredura de cada unidade em horários escalonados, fora da janela
    /// 08h–15h em que o SISREG bloqueia a exportação da agenda. Deixa o aviso por WhatsApp
    /// desligado em todas.
    /// </summary>
    [HttpPost("preparar-rede")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<PrepararRedeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<PrepararRedeDto> PrepararRede(
        [FromBody] PrepararRedeRequest request, CancellationToken cancellationToken) =>
        await _lote.PrepararRedeAsync(request, cancellationToken);

    /// <summary>
    /// Prévia da distribuição, sem gravar nada: a que horas a fila termina com essa hora inicial e
    /// esse intervalo, e quantas unidades não cabem na madrugada.
    /// </summary>
    [HttpPost("prever-agendamento")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<PreverAgendamentoDto>(StatusCodes.Status200OK)]
    public async Task<PreverAgendamentoDto> PreverAgendamento(
        [FromBody] PreverAgendamentoRequest request, CancellationToken cancellationToken) =>
        await _lote.PreverAgendamentoAsync(request, cancellationToken);

    /// <summary>
    /// Liga ou desliga a importação diária de <b>todas</b> as unidades de uma vez. Desligar não
    /// apaga horário nenhum: as agendas ficam prontas para religar.
    /// </summary>
    [HttpPut("agendamento-rede")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<AlternarAgendamentoRedeDto>(StatusCodes.Status200OK)]
    public async Task<AlternarAgendamentoRedeDto> AlternarAgendamentoRede(
        [FromBody] AlternarAgendamentoRedeRequest request, CancellationToken cancellationToken) =>
        await _lote.AlternarAgendamentoRedeAsync(request.Ativo, cancellationToken);

    /// <summary>Configuração do disparo diário automático.</summary>
    [HttpGet("agendamento")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<MapeamentoLoteAgendamentoDto>(StatusCodes.Status200OK)]
    public async Task<MapeamentoLoteAgendamentoDto> ObterAgendamento(CancellationToken cancellationToken) =>
        await _lote.ObterAgendamentoAsync(cancellationToken);

    /// <summary>Liga/desliga o disparo diário e define a hora (Brasília, HH:mm).</summary>
    [HttpPut("agendamento")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<MapeamentoLoteAgendamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<MapeamentoLoteAgendamentoDto> SalvarAgendamento(
        [FromBody] SalvarMapeamentoLoteAgendamentoRequest request, CancellationToken cancellationToken) =>
        await _lote.SalvarAgendamentoAsync(request, cancellationToken);
}

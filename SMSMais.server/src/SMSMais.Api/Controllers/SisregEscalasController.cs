using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.SisregWeb.Escalas;
using SMSMais.Core.Integracoes.SisregWeb.Escalas.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Sincronismo da grade de ESCALAS do SISREG — a oferta de vagas (profissional × unidade ×
/// procedimento × dia da semana), que é a base da Agenda.
///
/// <para><b>Não exige unidade selecionada</b>, ao contrário da varredura de agenda: a tela
/// <c>cons_escalas</c> aceita recorte sem critério e uma requisição traz a rede inteira.</para>
///
/// <para><b>Somente leitura no SISREG.</b> A tela tem um <c>editarEscala()</c> no JS; nada aqui o
/// aciona — mexer na grade de vagas do município exige decisão que não é de um robô.</para>
/// </summary>
[ApiController]
[Route("sisreg/escalas")]
public sealed class SisregEscalasController(IEscalasSincronizacaoService escalas) : ControllerBase
{
    private readonly IEscalasSincronizacaoService _escalas = escalas;

    /// <summary>Dispara a sincronização agora. 202: roda no servidor, fechar a aba não interrompe.</summary>
    [HttpPost("sincronizar")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<EscalasSincronizacaoAceitaDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Sincronizar(CancellationToken cancellationToken)
    {
        var aceita = await _escalas.IniciarAsync(cancellationToken);
        return Accepted(aceita);
    }

    /// <summary>Progresso da sincronização em curso. 204 quando não há nenhuma.</summary>
    [HttpGet("status")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<EscalasSincronizacaoStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Status()
    {
        var status = _escalas.ObterStatus();
        return status is null ? NoContent() : Ok(status);
    }

    [HttpPost("cancelar")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<object>(StatusCodes.Status200OK)]
    public IActionResult Cancelar() => Ok(new { cancelada = _escalas.Cancelar() });

    /// <summary>Sincronizações recentes — o que sobra depois que o progresso vivo some.</summary>
    [HttpGet("execucoes")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<EscalasSincronizacaoExecucaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<EscalasSincronizacaoExecucaoDto>> Execucoes(
        [FromQuery] int limite = 10, CancellationToken cancellationToken = default) =>
        await _escalas.ListarExecucoesAsync(limite, cancellationToken);

    [HttpGet("agendamento")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<EscalasAgendamentoDto>(StatusCodes.Status200OK)]
    public async Task<EscalasAgendamentoDto> ObterAgendamento(CancellationToken cancellationToken) =>
        await _escalas.ObterAgendamentoAsync(cancellationToken);

    /// <summary>Liga/desliga o disparo diário e ajusta a hora (Brasília).</summary>
    [HttpPut("agendamento")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<EscalasAgendamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<EscalasAgendamentoDto> SalvarAgendamento(
        [FromBody] SalvarEscalasAgendamentoRequest request, CancellationToken cancellationToken) =>
        await _escalas.SalvarAgendamentoAsync(request, cancellationToken);
}

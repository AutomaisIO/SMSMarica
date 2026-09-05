using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.SisregWeb.Alteracoes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Fila do que o SISREG mudou em agendamentos que já estavam aqui — remarcação, troca de
/// profissional ou de procedimento.
///
/// <para>Existe para que o agente de regulação e a unidade solicitante vejam a mudança e tomem
/// providência. Antes disso, uma consulta remarcada no SISREG deixava nosso banco com a data velha
/// indefinidamente, e nada denunciava.</para>
/// </summary>
[ApiController]
[Route("alteracoes-agenda")]
public sealed class AlteracoesAgendaController(IAlteracoesAgendaService alteracoes) : ControllerBase
{
    private readonly IAlteracoesAgendaService _alteracoes = alteracoes;

    /// <summary>A fila, mais recentes primeiro e pendentes na frente.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.AlteracoesAgenda, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaAlteracoesAgendaDto>(StatusCodes.Status200OK)]
    public async Task<PaginaAlteracoesAgendaDto> Listar(
        [FromQuery] bool apenasPendentes = true,
        [FromQuery] int pagina = 0,
        [FromQuery] int tamanho = 50,
        CancellationToken cancellationToken = default) =>
        await _alteracoes.ListarAsync(apenasPendentes, pagina, tamanho, cancellationToken);

    /// <summary>Marca como resolvida, sem avisar o paciente.</summary>
    [HttpPost("{id:guid}/tratar")]
    [RequerPermissao(ModuloPermissao.AlteracoesAgenda, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Tratar(Guid id, CancellationToken cancellationToken)
    {
        await _alteracoes.TratarAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Reenvia a confirmação ao paciente com os dados atuais e marca a alteração como tratada.
    /// <b>Revoga os links anteriores</b> — quem tem na mão a data velha perde o acesso a ela.
    /// </summary>
    [HttpPost("{id:guid}/comunicar")]
    [RequerPermissao(ModuloPermissao.AlteracoesAgenda, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Comunicar(Guid id, CancellationToken cancellationToken)
    {
        await _alteracoes.ComunicarAsync(id, cancellationToken);
        return NoContent();
    }
}

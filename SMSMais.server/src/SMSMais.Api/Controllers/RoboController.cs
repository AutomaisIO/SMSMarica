using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.RoboAtendimento;
using SMSMais.Core.RoboAtendimento.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Robô de atendimento (WhatsApp): cadastro dos ASSUNTOS que o robô atende (com treinos,
/// condições e comandos liberados) e a configuração global. Atender/ver conversas continua
/// sendo o módulo Conversas — aqui é só a gestão do bot.
/// </summary>
[ApiController]
[Route("robo")]
public sealed class RoboController(
    IRoboAssuntoService assuntos,
    IRoboConfiguracaoService configuracao,
    IRoboErroService erros,
    SMSMais.Core.RoboAtendimento.Runtime.IRoboSimulacaoService simulacao) : ControllerBase
{
    // ---- Assuntos ----

    [HttpGet("assuntos")]
    [RequerPermissao(ModuloPermissao.RoboAtendimento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<RoboAssuntoListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<RoboAssuntoListItemDto>> Listar(
        [FromQuery] bool incluirInativos, CancellationToken ct) =>
        await assuntos.ListarAsync(incluirInativos, ct);

    [HttpGet("assuntos/catalogo-comandos")]
    [RequerPermissao(ModuloPermissao.RoboAtendimento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ComandoRoboCatalogoDto>>(StatusCodes.Status200OK)]
    public IReadOnlyList<ComandoRoboCatalogoDto> CatalogoComandos() =>
        assuntos.ListarCatalogoComandos();

    [HttpGet("assuntos/{id:guid}")]
    [RequerPermissao(ModuloPermissao.RoboAtendimento, AcoesPermissao.Consulta)]
    [ProducesResponseType<RoboAssuntoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RoboAssuntoDto> Obter(Guid id, CancellationToken ct) =>
        await assuntos.ObterAsync(id, ct);

    [HttpPost("assuntos")]
    [RequerPermissao(ModuloPermissao.RoboAtendimento, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Criar([FromBody] SalvarRoboAssuntoRequest request, CancellationToken ct)
    {
        var id = await assuntos.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id }, id);
    }

    [HttpPut("assuntos/{id:guid}")]
    [RequerPermissao(ModuloPermissao.RoboAtendimento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] SalvarRoboAssuntoRequest request, CancellationToken ct)
    {
        await assuntos.AtualizarAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("assuntos/{id:guid}")]
    [RequerPermissao(ModuloPermissao.RoboAtendimento, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await assuntos.ExcluirAsync(id, ct);
        return NoContent();
    }

    // ---- Configuração global ----

    [HttpGet("configuracao")]
    [RequerPermissao(ModuloPermissao.RoboAtendimento, AcoesPermissao.Consulta)]
    [ProducesResponseType<RoboConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<RoboConfiguracaoDto> ObterConfiguracao(CancellationToken ct) =>
        await configuracao.ObterAsync(ct);

    [HttpPut("configuracao")]
    [RequerPermissao(ModuloPermissao.RoboAtendimento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SalvarConfiguracao([FromBody] SalvarRoboConfiguracaoRequest request, CancellationToken ct)
    {
        await configuracao.SalvarAsync(request, ct);
        return NoContent();
    }

    // ---- Simulação (ensaio sem falar com o cidadão) ----

    /// <summary>
    /// Roda um turno do robô pela Messages API e devolve o que ele responderia, os comandos que
    /// chamaria, tokens, custo e latência. NADA é enviado ao cidadão e comandos de escrita não
    /// executam — é o ensaio que antecede religar o robô.
    /// </summary>
    [HttpPost("simular")]
    [RequerPermissao(ModuloPermissao.RoboAtendimento, AcoesPermissao.Edicao)]
    [ProducesResponseType<RoboSimulacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<RoboSimulacaoDto> Simular([FromBody] SimularRoboRequest request, CancellationToken ct) =>
        await simulacao.SimularAsync(request, ct);

    // ---- Erros para treinamento ----

    /// <summary>Lista os erros do robô marcados por atendentes (para revisão/treinamento).</summary>
    [HttpGet("erros")]
    [RequerPermissao(ModuloPermissao.RoboAtendimento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<RoboErroDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<RoboErroDto>> ListarErros(
        [FromQuery] StatusRoboErro? status, [FromQuery] Guid? assuntoId, CancellationToken ct) =>
        await erros.ListarAsync(status, assuntoId, ct);

    /// <summary>Revisa um erro marcado (Revisado ou Descartado).</summary>
    [HttpPost("erros/{id:guid}/revisar")]
    [RequerPermissao(ModuloPermissao.RoboAtendimento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevisarErro(Guid id, [FromBody] RevisarRoboErroRequest request, CancellationToken ct)
    {
        await erros.RevisarAsync(id, request, ct);
        return NoContent();
    }
}

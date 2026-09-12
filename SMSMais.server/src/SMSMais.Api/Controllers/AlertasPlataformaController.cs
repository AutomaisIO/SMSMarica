using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Alertas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Avisos de erro da plataforma no celular: quem recebe, o que é reportado (e o que está
/// silenciado) e o histórico do que saiu. Mesma permissão da tela de Erros do sistema — é o
/// mesmo público (quem cuida da plataforma).
/// </summary>
[ApiController]
[Route("alertas-plataforma")]
public sealed class AlertasPlataformaController(IAlertaPlataformaService service) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Consulta)]
    [ProducesResponseType<AlertaPainelDto>(StatusCodes.Status200OK)]
    public Task<AlertaPainelDto> Painel(CancellationToken cancellationToken) =>
        service.ObterPainelAsync(cancellationToken);

    /// <summary>Histórico de avisos, opcionalmente de uma fonte só.</summary>
    [HttpGet("envios")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AlertaEnvioDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AlertaEnvioDto>> Envios(
        [FromQuery] string? origem, [FromQuery] int limite = 100, CancellationToken cancellationToken = default) =>
        service.ListarEnviosAsync(origem, limite, cancellationToken);

    [HttpPost("destinatarios")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType<AlertaDestinatarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<AlertaDestinatarioDto> AdicionarDestinatario(
        [FromBody] SalvarAlertaDestinatarioRequest request, CancellationToken cancellationToken) =>
        service.AdicionarDestinatarioAsync(request, cancellationToken);

    [HttpPut("destinatarios/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType<AlertaDestinatarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<AlertaDestinatarioDto> AtualizarDestinatario(
        Guid id, [FromBody] SalvarAlertaDestinatarioRequest request, CancellationToken cancellationToken) =>
        service.AtualizarDestinatarioAsync(id, request, cancellationToken);

    [HttpDelete("destinatarios/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoverDestinatario(Guid id, CancellationToken cancellationToken)
    {
        await service.RemoverDestinatarioAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Silencia (ou volta a reportar) uma fonte. Silenciada continua sendo contada.</summary>
    [HttpPut("origens/{chave}/silencio")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType<AlertaOrigemDto>(StatusCodes.Status200OK)]
    public Task<AlertaOrigemDto> Silenciar(
        string chave, [FromBody] SilenciarAlertaOrigemRequest request, CancellationToken cancellationToken) =>
        service.SilenciarAsync(chave, request.Silenciada, cancellationToken);

    /// <summary>Manda um aviso de teste agora e devolve o que a Meta respondeu para cada número.</summary>
    [HttpPost("testar")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType<AlertaEnvioDto>(StatusCodes.Status200OK)]
    public Task<AlertaEnvioDto> Testar(CancellationToken cancellationToken) =>
        service.TestarAsync(cancellationToken);
}
